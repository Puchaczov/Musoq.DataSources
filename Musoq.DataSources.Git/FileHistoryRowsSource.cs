using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.DataSources;
using Musoq.Schema.Diagnostics;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git;

/// <summary>
/// Streams file changes in commit order. The CLI reader consumes Git's raw NUL protocol and never enumerates HEAD or
/// starts one history walk per path. The libgit2 reader is retained as an explicitly selected compatibility route.
/// </summary>
internal sealed class FileHistoryRowsSource : GitDiagnosticRowsSourceBase<FileHistoryEntity>
{
    private const int ChunkSize = 128;
    private const int MaximumRetainedChanges = 1_024;
    private readonly Func<string, Repository> _createRepository;
    private readonly string _filePattern;
    private readonly GitHistoryBackendOptions _options;
    private readonly string _repositoryPath;
    private readonly GitProjection _projection;
    private readonly int _skip;
    private readonly int _take;

    public FileHistoryRowsSource(
        string repositoryPath,
        string filePattern,
        int skip,
        int take,
        Func<string, Repository> createRepository,
        SourceExecutionContext executionContext)
        : base(executionContext, "git.filehistory")
    {
        if (skip < 0)
            throw new ArgumentOutOfRangeException(nameof(skip), skip, "File history skip must be zero or greater.");

        _repositoryPath = repositoryPath;
        _filePattern = filePattern;
        _skip = skip;
        _take = take;
        _createRepository = createRepository;
        _options = GitHistoryBackendOptions.From(executionContext.SourceRuntimeSettings);
        _projection = GitSourcePlanner.GetProjection(executionContext.Plan);
    }

    protected override long CollectRows(DiagnosticChunkWriter<FileHistoryEntity> writer, CancellationToken cancellationToken)
    {
        if (_take == 0 || Context.Plan.AcceptedTake == 0)
        {
            Report("none", commitsExamined: 0, changesExamined: 0, rowsEmitted: 0, earlyStop: true);
            return 0;
        }

        var matcher = new FileHistoryMatcher(_filePattern, _repositoryPath);
        var fromOldest = _take < 0;
        var actualTake = _take < 0 ? -(long)_take : _take;
        var metadataPlan = FileHistoryMetadataPlan.Create(_projection);
        var chunk = new List<FileHistoryEntity>(ChunkSize);
        long changesExamined = 0;
        long commitsExamined = 0;
        long rowsEmitted = 0;
        long intrinsicRows = 0;
        long skipped = 0;
        long outerSkipped = 0;
        var earlyStop = false;

        bool ProcessCommit(CommitMetadata metadata, List<RawFileChange> changes)
        {
            commitsExamined++;
            // Git's raw diff order is not a public compatibility contract. Keep the documented ordinal path order
            // until the independent oracle demonstrates an equivalent no-sort route on all benchmark corpora.
            if (changes.Count > 1)
                changes.Sort(RawFileChangePathComparer.Instance);

            foreach (var change in changes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                changesExamined++;
                if (!matcher.IsMatch(change))
                    continue;

                if (skipped < _skip)
                {
                    skipped++;
                    continue;
                }

                intrinsicRows++;

                if (Context.Plan.AcceptedSkip.HasValue && outerSkipped < Context.Plan.AcceptedSkip.Value)
                {
                    outerSkipped++;
                }
                else
                {
                    chunk.Add(GitEntitySnapshots.FileHistory(
                        metadata.Sha,
                        metadata.Author,
                        metadata.AuthorEmail,
                        metadata.CommittedWhen,
                        change.FilePath,
                        change.ChangeType,
                        change.OldPath,
                        _projection));
                    rowsEmitted++;

                    if (chunk.Count == ChunkSize)
                    {
                        _ = WriteChunk(writer, chunk, rowsEmitted - ChunkSize);
                        Context.Diagnostics.AddRowsProduced(ChunkSize);
                    }
                }

                if (intrinsicRows == actualTake ||
                    Context.Plan.AcceptedTake.HasValue && rowsEmitted == Context.Plan.AcceptedTake.Value)
                {
                    earlyStop = true;
                    return false;
                }
            }

            return true;
        }

        var selectedBackend = _options.Backend == GitHistoryBackend.LibGit2 ? "libgit2" : "git-cli";

        try
        {
            switch (_options.Backend)
            {
                case GitHistoryBackend.LibGit2:
                    ReadWithLibGit2(fromOldest, metadataPlan, cancellationToken, ProcessCommit);
                    break;
                case GitHistoryBackend.Auto:
                case GitHistoryBackend.GitCli:
                    ReadWithGitCli(matcher, metadataPlan, fromOldest, cancellationToken, ProcessCommit, () => earlyStop = true);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported Git history backend '{_options.Backend}'.");
            }
        }
        finally
        {
            if (chunk.Count > 0)
            {
                _ = WriteChunk(writer, chunk, rowsEmitted - chunk.Count);
                Context.Diagnostics.AddRowsProduced(chunk.Count);
            }

            Report(
                selectedBackend,
                commitsExamined,
                changesExamined,
                rowsEmitted,
                earlyStop,
                fromOldest);
        }

        return rowsEmitted;
    }

    private void ReadWithGitCli(
        FileHistoryMatcher matcher,
        FileHistoryMetadataPlan metadataPlan,
        bool fromOldest,
        CancellationToken cancellationToken,
        Func<CommitMetadata, List<RawFileChange>, bool> processCommit,
        Action onEarlyStop)
    {
        var arguments = new List<string>
        {
            "log",
            "--topo-order",
            "--no-merges",
            "--root",
            "--raw",
            "-z",
            "--no-abbrev",
            "--no-ext-diff",
            "--find-renames=50%",
            "--find-copies=50%",
            "--format=" + metadataPlan.GitFormat
        };

        if (fromOldest)
            arguments.Add("--reverse");

        // Git's follow implementation is a single-path history walk and preserves rename semantics for exact paths.
        // Wildcards deliberately use the global raw traversal so deleted paths and both sides of every rename/copy are
        // visible to the compatibility matcher.
        if (matcher.ExactFullPath is not null)
        {
            arguments.Add("--follow");
            arguments.Add("--");
            arguments.Add(matcher.ExactFullPath);
        }

        using var process = GitCliProcess.Start(_repositoryPath, _options, arguments, cancellationToken);
        using var reader = new GitNulDelimitedUtf8Reader(process.StandardOutput);
        string? pendingHeader = null;
        var completedNaturally = true;
        var changes = new List<RawFileChange>(ChunkSize);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var header = pendingHeader ?? reader.ReadToken();
            pendingHeader = null;
            if (header is null)
                break;
            header = NormalizeRecordToken(header);
            if (header.Length == 0)
                continue;
            if (!header.StartsWith('\u001e'))
                throw new InvalidDataException("Git raw log protocol lost its commit record delimiter.");

            var metadata = metadataPlan.Read(header, reader);
            changes.Clear();

            while (true)
            {
                var token = reader.ReadToken();
                if (token is null)
                    break;
                token = NormalizeRecordToken(token);
                if (token.Length == 0)
                    continue;
                if (token.StartsWith('\u001e'))
                {
                    pendingHeader = token;
                    break;
                }
                if (!token.StartsWith(':'))
                    throw new InvalidDataException($"Unexpected Git raw change record '{token}'.");

                changes.Add(ReadChange(token, reader));
            }

            if (!processCommit(metadata, changes))
            {
                completedNaturally = false;
                onEarlyStop();
                process.Stop();
                break;
            }

            changes = ShrinkIfOversized(changes);
        }

        if (completedNaturally)
            process.Complete();
    }

    private void ReadWithLibGit2(
        bool fromOldest,
        FileHistoryMetadataPlan metadataPlan,
        CancellationToken cancellationToken,
        Func<CommitMetadata, List<RawFileChange>, bool> processCommit)
    {
        using var repository = _createRepository(_repositoryPath);
        var filter = new CommitFilter { SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time };
        var commits = repository.Commits.QueryBy(filter);
        var orderedCommits = fromOldest ? commits.Reverse() : commits;
        var changes = new List<RawFileChange>(ChunkSize);

        foreach (var commit in orderedCommits)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (commit.Parents.Count() > 1)
                continue;

            var parent = commit.Parents.SingleOrDefault();
            changes.Clear();
            foreach (var change in repository.Diff.Compare<TreeChanges>(parent?.Tree, commit.Tree))
                changes.Add(new RawFileChange(change.Path, change.OldPath, change.Status.ToString()));
            var metadata = metadataPlan.Create(commit);

            if (!processCommit(metadata, changes))
                break;

            changes = ShrinkIfOversized(changes);
        }
    }

    private void Report(
        string backend,
        long commitsExamined,
        long changesExamined,
        long rowsEmitted,
        bool earlyStop,
        bool fullHistoryRequired = false)
    {
        Context.Diagnostics.AddMetric("Git.FileHistory.Backend", backend == "git-cli" ? 1 : backend == "libgit2" ? 2 : 0);
        Context.Diagnostics.AddMetric("Git.FileHistory.CommitsExamined", commitsExamined);
        Context.Diagnostics.AddMetric("Git.FileHistory.ChangesExamined", changesExamined);
        Context.Diagnostics.AddMetric("Git.FileHistory.RowsEmitted", rowsEmitted);
        Context.Diagnostics.AddMetric("Git.FileHistory.EarlyStop", earlyStop ? 1 : 0);
        Context.Diagnostics.AddMetric("Git.FileHistory.FullHistoryRequired", fullHistoryRequired ? 1 : 0);
        Context.Logger.LogInformation(
            "Git filehistory completed with backend {Backend}; commits examined {CommitsExamined}; changes examined {ChangesExamined}; rows emitted {RowsEmitted}; early stop {EarlyStop}; full-history required {FullHistoryRequired}.",
            backend,
            commitsExamined,
            changesExamined,
            rowsEmitted,
            earlyStop,
            fullHistoryRequired);
    }

    private static RawFileChange ReadChange(string metadata, GitNulDelimitedUtf8Reader reader)
    {
        var parts = metadata[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
            throw new InvalidDataException($"Git emitted an invalid raw change header '{metadata}'.");

        var status = parts[4];
        var firstPath = reader.ReadToken() ?? throw new InvalidDataException("Git raw log ended while reading a changed path.");
        if (status.Length > 0 && status[0] is 'R' or 'C')
        {
            var secondPath = reader.ReadToken() ?? throw new InvalidDataException("Git raw log ended while reading a rename/copy path.");
            return new RawFileChange(secondPath, firstPath, ChangeType(status[0]));
        }

        return new RawFileChange(firstPath, null, ChangeType(status[0]));
    }

    private static string ChangeType(char status) => status switch
    {
        'A' => "Added",
        'M' => "Modified",
        'D' => "Deleted",
        'R' => "Renamed",
        'C' => "Copied",
        'T' => "TypeChanged",
        _ => status.ToString()
    };

    private static string NormalizeRecordToken(string token) =>
        token.Length > 1 && token[0] == '\n' && token[1] is ':' or '\u001e'
            ? token[1..]
            : token;

    private static List<RawFileChange> ShrinkIfOversized(List<RawFileChange> changes) =>
        changes.Capacity <= MaximumRetainedChanges
            ? changes
            : new List<RawFileChange>(ChunkSize);

    private readonly record struct CommitMetadata(string Sha, string Author, string AuthorEmail, DateTimeOffset CommittedWhen);

    private readonly record struct RawFileChange(string FilePath, string? OldPath, string ChangeType);

    private sealed class RawFileChangePathComparer : IComparer<RawFileChange>
    {
        public static RawFileChangePathComparer Instance { get; } = new();

        public int Compare(RawFileChange left, RawFileChange right) =>
            StringComparer.Ordinal.Compare(left.FilePath, right.FilePath);
    }

    /// <summary>
    /// Shapes the raw Git record to only the requested metadata. File paths and change kinds remain mandatory for
    /// matching and ordering; they are parsed from raw diff records rather than the commit header.
    /// </summary>
    private readonly record struct FileHistoryMetadataPlan(
        bool IncludesSha,
        bool IncludesAuthor,
        bool IncludesAuthorEmail,
        bool IncludesCommittedWhen)
    {
        public string GitFormat
        {
            get
            {
                var format = new StringBuilder("tformat:%x1e");
                if (IncludesSha)
                    format.Append("%H");
                format.Append("%x00");
                if (IncludesAuthor)
                    format.Append("%an%x00");
                if (IncludesAuthorEmail)
                    format.Append("%ae%x00");
                if (IncludesCommittedWhen)
                    format.Append("%cI%x00");
                return format.ToString();
            }
        }

        public static FileHistoryMetadataPlan Create(GitProjection projection) => new(
            projection.Includes(nameof(FileHistoryEntity.CommitSha)),
            projection.Includes(nameof(FileHistoryEntity.Author)),
            projection.Includes(nameof(FileHistoryEntity.AuthorEmail)),
            projection.Includes(nameof(FileHistoryEntity.CommittedWhen)));

        public CommitMetadata Read(string header, GitNulDelimitedUtf8Reader reader)
        {
            var sha = IncludesSha ? header[1..] : string.Empty;
            var author = IncludesAuthor ? ReadRequired(reader, "author") : string.Empty;
            var authorEmail = IncludesAuthorEmail ? ReadRequired(reader, "author email") : string.Empty;
            var committedWhen = IncludesCommittedWhen
                ? ReadCommittedWhen(ReadRequired(reader, "commit timestamp"))
                : default;
            return new CommitMetadata(sha, author, authorEmail, committedWhen);
        }

        public CommitMetadata Create(Commit commit) => new(
            IncludesSha ? commit.Sha : string.Empty,
            IncludesAuthor ? commit.Author.Name : string.Empty,
            IncludesAuthorEmail ? commit.Author.Email : string.Empty,
            IncludesCommittedWhen ? commit.Committer.When : default);

        private static string ReadRequired(GitNulDelimitedUtf8Reader reader, string field) =>
            reader.ReadToken() ?? throw new InvalidDataException($"Git raw log ended while reading the {field}.");

        private static DateTimeOffset ReadCommittedWhen(string value)
        {
            if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var committedWhen))
                throw new InvalidDataException($"Git emitted an invalid ISO-8601 commit timestamp '{value}'.");
            return committedWhen;
        }
    }

    private sealed class FileHistoryMatcher
    {
        private readonly Regex? _wildcardRegex;
        private readonly bool _isFullPathPattern;
        private readonly bool _isWildcardPattern;
        private readonly string _pattern;

        public FileHistoryMatcher(string pattern, string repositoryPath)
        {
            _pattern = NormalizePathToRepositoryRelative(pattern, repositoryPath).Replace('\\', '/');
            _isFullPathPattern = _pattern.Contains('/');
            _isWildcardPattern = _pattern.Contains('*') || _pattern.Contains('?');
            if (_isWildcardPattern)
            {
                var regexPattern = "^" + Regex.Escape(_pattern)
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") + "$";
                _wildcardRegex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
        }

        public string? ExactFullPath => !_isWildcardPattern && _isFullPathPattern ? _pattern : null;

        public bool IsMatch(RawFileChange change) =>
            IsPathMatch(change.FilePath) || change.OldPath is not null && IsPathMatch(change.OldPath);

        private bool IsPathMatch(string path)
        {
            var normalizedPath = path.Replace('\\', '/');
            var target = _isFullPathPattern ? normalizedPath : FileName(normalizedPath);
            if (_isWildcardPattern)
                return _wildcardRegex!.IsMatch(target);

            return string.Equals(target, _pattern, StringComparison.OrdinalIgnoreCase);
        }

        private static string FileName(string path)
        {
            var index = path.LastIndexOf('/');
            return index < 0 ? path : path[(index + 1)..];
        }

        private static string NormalizePathToRepositoryRelative(string pattern, string repositoryPath)
        {
            if (!Path.IsPathRooted(pattern))
                return pattern;

            var normalizedRepositoryPath = Path.GetFullPath(repositoryPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedPattern = Path.GetFullPath(pattern);
            if (normalizedPattern.StartsWith(normalizedRepositoryPath, StringComparison.OrdinalIgnoreCase))
                return normalizedPattern[normalizedRepositoryPath.Length..]
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return pattern;
        }
    }

}

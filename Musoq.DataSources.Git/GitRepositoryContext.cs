using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.Git;

/// <summary>Managed execution identity shared by detached rows; owns no native handle or process.</summary>
internal sealed partial class GitRepositoryContext
{
    private readonly IReadOnlyDictionary<string, string> _settings;
    private readonly Lazy<bool> _requiresCli;
    private bool _forceCli;
    public GitRepositoryLocation Location { get; }
    public CancellationToken Token { get; }
    public SourceDiagnostics Diagnostics { get; }
    public bool RequiresCli => Volatile.Read(ref _forceCli) || _requiresCli.Value;
    public GitHistoryBackendOptions HistoryOptions => GitHistoryBackendOptions.From(_settings);
    public GitReferenceBackendOptions ReferenceOptions => GitReferenceBackendOptions.From(_settings);

    public GitRepositoryContext(GitRepositoryLocation location, IReadOnlyDictionary<string, string> settings,
        CancellationToken token, SourceDiagnostics? diagnostics = null)
    {
        Location = location;
        _settings = settings;
        Token = token;
        Diagnostics = diagnostics ?? SourceDiagnostics.None;
        _requiresCli = new Lazy<bool>(() =>
        {
            if (location.GitDirectory == location.CommonDirectory &&
                !Directory.Exists(Path.Combine(location.CommonDirectory, "worktrees"))) return false;
            var configPath = Path.Combine(location.CommonDirectory, "config");
            if (!File.Exists(configPath)) return false;
            var text = File.ReadAllText(configPath);
            if (!text.Contains("relativeworktrees", StringComparison.OrdinalIgnoreCase) &&
                !text.Contains("include", StringComparison.OrdinalIgnoreCase)) return false;
            using var config = LibGit2Sharp.Configuration.BuildFrom(configPath);
            return config.Get<bool>("extensions.relativeworktrees")?.Value == true;
        });
    }

    internal bool TryEnableCli(LibGit2SharpException exception)
    {
        if (!exception.Message.StartsWith("unsupported extension name extensions.relativeworktrees", StringComparison.OrdinalIgnoreCase))
            return false;
        using var configuration = LibGit2Sharp.Configuration.BuildFrom(Path.Combine(Location.CommonDirectory, "config"));
        if (configuration.Get<bool>("extensions.relativeworktrees")?.Value != true) return false;
        Volatile.Write(ref _forceCli, true);
        return true;
    }

    public void EnsureBackend(string source)
    {
        var backend = source switch
        {
            "git.tags" or "git.stashes" or "git.remotetags" => ReferenceOptions.Backend,
            "git.filehistory" => HistoryOptions.Backend,
            _ => GitHistoryBackend.Auto
        };
        if (backend == GitHistoryBackend.LibGit2 && RequiresCli)
            throw new InvalidOperationException("This repository uses extensions.relativeWorktrees, which the bundled libgit2 does not support. Select auto or git-cli.");
    }

    internal IGitCliProcess Start(params string[] arguments)
        => Start(arguments, null);

    private IGitCliProcess Start(string[] arguments, string? standardInput)
    {
        Token.ThrowIfCancellationRequested();
        return GitCliProcess.Start(new GitCliProcessRequest(Location.ExecutionRoot, GitHistoryBackendOptions.FromExecutable(_settings).Executable,
            string.Empty, arguments, Location: Location, StandardInput: standardInput), Token);
    }

    internal string Text(params string[] arguments)
    {
        using var process = Start(arguments);
        using var reader = new StreamReader(process.StandardOutput, Encoding.UTF8);
        var text = reader.ReadToEnd();
        process.Complete();
        return text;
    }

    internal string? OptionalText(params string[] arguments)
    {
        using var process = Start(arguments);
        using var reader = new StreamReader(process.StandardOutput, Encoding.UTF8);
        var text = reader.ReadToEnd();
        try { process.Complete(); }
        catch (InvalidOperationException) when (process.ExitCode == 1) { return null; }
        return text;
    }

    internal IEnumerable<string> Tokens(params string[] arguments) => Tokens(arguments, null);

    private IEnumerable<string> Tokens(string[] arguments, string? standardInput)
    {
        using var process = Start(arguments, standardInput);
        using var reader = new GitNulDelimitedUtf8Reader(process.StandardOutput);
        while (reader.ReadToken() is { } field) { Token.ThrowIfCancellationRequested(); yield return field; }
        process.Complete();
    }

    private static string Required(IEnumerator<string> fields)
    {
        if (!fields.MoveNext()) throw new InvalidDataException("Git output ended in the middle of a record.");
        return fields.Current;
    }

    private static string DirectoryPath(string path) => Path.TrimEndingDirectorySeparator(path) + Path.DirectorySeparatorChar;

    public RepositoryEntity Repository()
    {
        var flags = Text("rev-parse", "--is-bare-repository", "--is-shallow-repository").Split('\n');
        var head = OptionalText("rev-parse", "--verify", "--quiet", "HEAD")?.Trim();
        var branch = OptionalText("symbolic-ref", "--quiet", "HEAD")?.Trim();
        var gitPath = DirectoryPath(Location.GitDirectory);
        var worktree = Location.WorktreeRoot is { } root ? DirectoryPath(root) : null;
        return new RepositoryEntity(gitPath, worktree!,
            new RepositoryInformationEntity(gitPath, worktree!, flags[0].Trim() == "true", branch is null,
                head is null, flags[1].Trim() == "true"), ReferenceOptions) { ReadContext = this };
    }

    public BranchEntity? Head()
    {
        if (!RequiresCli)
        {
            using var repository = OpenNative();
            return repository.Head is { } nativeHead ? NativeBranch(nativeHead, repository) : null;
        }
        var branch = OptionalText("symbolic-ref", "--quiet", "HEAD")?.Trim();
        if (branch is not null)
            return Branches().FirstOrDefault(b => b.CanonicalName == branch) ??
                new BranchEntity(Location.GitDirectory, branch["refs/heads/".Length..], branch, false, false,
                    true, null, null, null, null, null, null) { ReadContext = this };
        var commit = OptionalText("rev-parse", "--verify", "--quiet", "HEAD")?.Trim();
        return new BranchEntity(Location.GitDirectory, "(no branch)", "(no branch)", false, false,
            true, null, null, null, commit, null, null) { ReadContext = this };
    }

    public IEnumerable<CommitEntity> Commits(string revision = "HEAD", bool single = false, string? exclude = null,
        bool topological = false, GitProjection? projection = null)
    {
        if (!RequiresCli)
        {
            foreach (var commit in NativeCommits(revision, single, exclude, topological)) yield return commit;
            yield break;
        }
        var fieldsProjection = projection is null || projection.NativePatterns is not null || projection.Includes("Self")
            ? GitProjection.NotAccepted : projection;
        var metadata = GitCommitMetadataPlan.Create(fieldsProjection);
        var args = new List<string> { "log", "--encoding=UTF-8", "-z", "--format=" + metadata.GitFormat };
        if (topological) args.Insert(1, "--date-order");
        string? standardInput = null;
        if (!topological && !single)
        {
            var revisions = new List<string> { "log", "--format=%H %ct", "--end-of-options", revision };
            if (exclude is not null) revisions.Add("^" + exclude);
            revisions.Add("--");
            try { standardInput = GitCommitTimeOrdering.CreateInput(Text(revisions.ToArray()), Token); }
            catch (InvalidOperationException exception) when (IsUnbornError(exception, revision)) { yield break; }
            if (standardInput.Length == 0) yield break;
            // Native nested histories sort the complete reachable graph by committer time, even with skewed
            // clocks. Order compact identities, then ask Git to stream metadata in that order.
            Diagnostics.AddMetric("Git.History.OrderedRevisionCharacters", standardInput.Length);
            args.AddRange(["--no-walk=unsorted", "--stdin"]);
        }
        if (single) args.AddRange(["--no-walk", "--max-count=1"]);
        if (standardInput is null)
        {
            args.Add("--end-of-options"); args.Add(revision);
            if (exclude is not null) args.Add("^" + exclude);
        }
        args.Add("--");
        using var process = Start(args.ToArray(), standardInput);
        using var reader = new GitNulDelimitedUtf8Reader(process.StandardOutput);
        while (reader.ReadToken() is { } header)
        {
            Token.ThrowIfCancellationRequested();
            header = header.TrimStart('\r', '\n');
            if (header.Length == 0) continue;
            if (!header.StartsWith('\u001e')) throw new InvalidDataException("Git commit protocol lost its record delimiter.");
            var entity = GitEntitySnapshots.Commit(metadata.Read(Location.GitDirectory, header, reader), fieldsProjection);
            entity.ReadContext = this;
            yield return entity;
        }
        try { process.Complete(); }
        catch (InvalidOperationException exception) when (IsUnbornError(exception, revision)) { }
    }

    internal bool IsUnbornError(InvalidOperationException exception, string revision)
    {
        if (!exception.Message.StartsWith("Git exited with code 128:", StringComparison.Ordinal) ||
            !(exception.Message.Contains("does not have any commits yet", StringComparison.Ordinal) ||
              exception.Message.Contains("ambiguous argument 'HEAD'", StringComparison.Ordinal) ||
              exception.Message.Contains("bad revision 'HEAD'", StringComparison.Ordinal))) return false;
        var head = File.ReadAllText(Path.Combine(Location.GitDirectory, "HEAD")).Trim();
        if (!head.StartsWith("ref: refs/heads/", StringComparison.Ordinal)) return false;
        var reference = head[5..];
        if (revision != "HEAD" && revision != reference) return false;
        if (reference.Contains("..", StringComparison.Ordinal) ||
            Directory.Exists(Path.Combine(Location.CommonDirectory, "reftable")) ||
            File.Exists(Path.Combine(Location.CommonDirectory, reference))) return false;
        var packed = Path.Combine(Location.CommonDirectory, "packed-refs");
        return !File.Exists(packed) || !File.ReadLines(packed).Any(line => line.EndsWith(" " + reference, StringComparison.Ordinal));
    }

    public CommitEntity? CommitFrom(string? revision)
    {
        if (string.IsNullOrWhiteSpace(revision)) return null;
        if (RequiresCli) return Commits(revision, single: true).FirstOrDefault();
        using var repository = OpenNative();
        var commit = repository.Lookup<Commit>(revision);
        return commit is null ? null : new CommitEntity(commit, repository) { ReadContext = this,
            ParentShas = commit.Parents.Select(p => p.Sha).ToArray() };
    }

    public IEnumerable<BranchEntity> Branches()
    {
        if (!RequiresCli)
        {
            foreach (var branch in NativeBranches()) yield return branch;
            yield break;
        }
        using var fields = Tokens("for-each-ref",
            "--format=%(refname)%00%(refname:short)%00%(objectname)%00%(upstream)%00%(upstream:remotename)%00%(HEAD)%00",
            "refs/heads", "refs/remotes").GetEnumerator();
        while (fields.MoveNext())
        {
            var canonical = fields.Current.TrimStart('\r', '\n');
            if (canonical.Length == 0) continue;
            var friendly = Required(fields); var tip = Required(fields); var upstream = Required(fields);
            var remote = Required(fields); var isHead = Required(fields) == "*";
            var isRemote = canonical.StartsWith("refs/remotes/", StringComparison.Ordinal);
            if (isRemote && remote.Length == 0) remote = canonical["refs/remotes/".Length..].Split('/')[0];
            yield return new BranchEntity(Location.GitDirectory, friendly, canonical, isRemote, upstream.Length > 0,
                isHead, upstream.Length == 0 ? null : upstream, null, null, tip,
                upstream.Length == 0 ? null : upstream, remote.Length == 0 ? null : remote, trackingLoaded: false) { ReadContext = this };
        }
    }

    public BranchEntity? BranchFrom(string? canonical) => string.IsNullOrWhiteSpace(canonical)
        ? null : Branches().FirstOrDefault(b => b.CanonicalName == canonical);

    public BranchTrackingDetailsEntity Tracking(BranchEntity branch)
    {
        Diagnostics.AddMetric("Git.Graph.TrackingQueries", 1);
        if (!RequiresCli)
        {
            using var repository = OpenNative();
            return new BranchTrackingDetailsEntity(repository.Branches[branch.CanonicalName]?.TrackingDetails!, repository);
        }
        if (branch.UpstreamBranchCanonicalName is not { } upstream) return new((int?)null, null);
        using var fields = Tokens("for-each-ref", "--format=%(refname)%00%(upstream:track)%00", branch.CanonicalName).GetEnumerator();
        while (fields.MoveNext())
        {
            var canonical = fields.Current.TrimStart('\r', '\n');
            if (canonical.Length == 0) continue;
            var tracking = Required(fields);
            if (canonical != branch.CanonicalName) continue;
            var counts = GitCliBranchReader.ParseTracking(tracking);
            return new(counts.AheadBy, counts.BehindBy);
        }
        return new((int?)null, null);
    }

    public IEnumerable<TagEntity> Tags(string? canonical = null)
    {
        EnsureBackend("git.tags");
        var query = GitTagReadQuery.Empty with { ExactCanonicalName = canonical };
        foreach (var record in new GitCliTagReader().ReadStreaming(Location.ExecutionRoot, ReferenceOptions,
                     GitProjection.NotAccepted, query, static _ => throw new InvalidOperationException(), Token))
        {
            var resolved = record;
            if (record.IsAnnotated && record.CommitSha is null)
            {
                var target = OptionalText("rev-parse", "--verify", "--quiet", "--end-of-options", record.CanonicalName + "^{}")?.Trim();
                var commit = OptionalText("rev-parse", "--verify", "--quiet", "--end-of-options", record.CanonicalName + "^{commit}")?.Trim();
                resolved = record with { TargetSha = target, CommitSha = commit };
            }
            var entity = GitEntitySnapshots.Tag(resolved, GitProjection.NotAccepted);
            entity.ReadContext = this;
            yield return entity;
        }
    }

    public IEnumerable<StashEntity> Stashes()
    {
        EnsureBackend("git.stashes");
        using var fields = Tokens("log", "-g", "--glob=refs/stas[h]", "--encoding=UTF-8", "-z", "--format=%gd%x00%H%x00%P%x00%gs%x00").GetEnumerator();
        while (fields.MoveNext())
        {
            var selector = fields.Current.TrimStart('\r', '\n');
            if (selector.Length == 0) continue;
            var sha = Required(fields); var parents = Required(fields).Split(' '); var message = Required(fields);
            yield return new StashEntity(Location.GitDirectory, selector, sha, message,
                parents.Length > 1 ? parents[1] : null, sha, parents.Length > 2 ? parents[2] : null) { ReadContext = this };
        }
    }

    public IEnumerable<ConfigurationEntityKeyValue> Configuration()
    {
        if (!RequiresCli)
        {
            using var repository = OpenNative();
            foreach (var entry in repository.Config)
            {
                Token.ThrowIfCancellationRequested();
                yield return new ConfigurationEntityKeyValue(entry, repository);
            }
            yield break;
        }
        using var fields = Tokens("config", "--null", "--show-scope", "--list").GetEnumerator();
        while (fields.MoveNext())
        {
            var scope = fields.Current; var entry = Required(fields); var newline = entry.IndexOf('\n');
            if (scope == "command") continue;
            var key = newline < 0 ? entry : entry[..newline];
            var value = newline < 0 ? string.Empty : entry[(newline + 1)..];
            yield return new ConfigurationEntityKeyValue(key, value, scope switch
                { "system" => "System", "global" => "Global", "local" => "Local", "worktree" => "Worktree", _ => "App" });
        }
    }

    public IEnumerable<RemoteEntity> Remotes()
    {
        var entries = Configuration().Where(c => c.Key.StartsWith("remote.", StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var url in entries.Where(c => c.Key.EndsWith(".url", StringComparison.OrdinalIgnoreCase)))
        {
            var name = url.Key[7..^4];
            yield return new RemoteEntity(name, url.Value, entries.FirstOrDefault(c => c.Key == "remote." + name + ".pushurl")?.Value ?? url.Value);
        }
    }

    public IEnumerable<StatusEntity> Status()
    {
        // Reuse the qualified porcelain parser; callback records are cheap and the status result is bounded by files.
        // The pull implementation below preserves cancellation and early-disposal without a producer queue.
        using var fields = Tokens("status", "--porcelain=v1", "-z", "--untracked-files=all").GetEnumerator();
        while (fields.MoveNext())
        {
            var field = fields.Current;
            if (field.Length < 3 || field[2] != ' ') throw new InvalidDataException("Invalid Git status record.");
            var state = GitCliStatusReader.ToFileStatus(field[0], field[1]);
            if (field[0] is 'R' or 'C' || field[1] is 'R' or 'C') _ = Required(fields);
            yield return new StatusEntity(field[3..], state.ToString(), GitEntitySnapshots.IndexStatus(state), GitEntitySnapshots.WorkDirStatus(state));
        }
    }

    public IEnumerable<T> Rows<T>(string source, string? directSha = null, GitProjection? projection = null) => source switch
    {
        "git.repository" => (IEnumerable<T>)(object)new[] { Repository() },
        "git.commits" => (IEnumerable<T>)(object)Commits(directSha ?? "HEAD", single: directSha is not null,
            topological: true, projection: projection),
        "git.branches" => (IEnumerable<T>)(object)Branches(),
        "git.tags" => (IEnumerable<T>)(object)Tags(),
        "git.stashes" => (IEnumerable<T>)(object)Stashes(),
        "git.remotes" => (IEnumerable<T>)(object)Remotes(),
        "git.status" => (IEnumerable<T>)(object)Status(),
        _ => throw new NotSupportedException($"The CLI compatibility reader for {source} is not available.")
    };
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using LibGit2Sharp;

namespace Musoq.DataSources.Git;

/// <summary>
/// Streaming, operation-specific commit reader contract. It intentionally exposes scalar records only, so the
/// source cannot accidentally retain a LibGit2Sharp handle while choosing between implementation backends.
/// </summary>
internal interface IGitCommitReader
{
    string Backend { get; }

    void Read(
        string repositoryPath,
        GitProjection projection,
        string? directSha,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken,
        Func<GitCommitRecord, bool> onCommit);
}

internal readonly record struct GitCommitRecord(
    string RepositoryPath,
    string Sha,
    string? Message,
    string? MessageShort,
    string? Author,
    string? AuthorEmail,
    string? Committer,
    string? CommitterEmail,
    DateTimeOffset CommittedWhen);

/// <summary>Backend selection is deliberately per operation and changes only after parity plus benchmark qualification.</summary>
internal static class GitOperationReaders
{
    public static IGitCommitReader LibGit2Commits { get; } = new LibGit2CommitReader();
    public static IGitCommitReader CliCommits { get; } = new GitCliCommitReader();
    public static IGitCommitReader Commits { get; } = LibGit2Commits;

    // References stay on LibGit2 until the CLI candidate meets the same parity and relative performance rule.
    public static IGitBranchReader Branches { get; } = new LibGit2BranchReader();
    public static IGitBranchReader CliBranches { get; } = new GitCliBranchReader();
    public static IGitTagReader Tags { get; } = new LibGit2TagReader();
    public static IGitTagReader CliTags { get; } = new GitCliTagReader();
    public static IGitRemoteReader Remotes { get; } = new LibGit2RemoteReader();
    public static IGitRemoteReader CliRemotes { get; } = new GitCliRemoteReader();
    public static IGitStatusReader Status { get; } = new LibGit2StatusReader();
    public static IGitStatusReader CliStatus { get; } = new GitCliStatusReader();
}

internal sealed class GitCliCommitReader : IGitCommitReader
{
    public string Backend => "git-cli";

    public void Read(
        string repositoryPath,
        GitProjection projection,
        string? directSha,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken,
        Func<GitCommitRecord, bool> onCommit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(onCommit);
        cancellationToken.ThrowIfCancellationRequested();

        var metadata = GitCommitMetadataPlan.Create(projection);
        var arguments = new List<string>
        {
            "log",
            "--topo-order",
            "--no-ext-diff",
            "-z",
            "--format=" + metadata.GitFormat
        };

        if (!string.IsNullOrWhiteSpace(directSha))
        {
            // An equality predicate on SHA is a direct object lookup, not a history scan. The evaluator predicate
            // remains authoritative, so an invalid/ambiguous object simply follows Git's normal actionable error.
            arguments.Add("--max-count=1");
            arguments.Add(directSha);
        }

        using var process = GitCliProcess.Start(repositoryPath, GitHistoryBackendOptions.Default, arguments, cancellationToken);
        using var reader = new GitNulDelimitedUtf8Reader(process.StandardOutput);
        var completedNaturally = true;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var header = reader.ReadToken();
            if (header is null)
                break;
            header = NormalizeRecordToken(header);
            if (header.Length == 0)
                continue;
            if (!header.StartsWith('\u001e'))
                throw new InvalidDataException("Git commit protocol lost its record delimiter.");

            if (!onCommit(metadata.Read(repositoryPath, header, reader)))
            {
                completedNaturally = false;
                process.Stop();
                break;
            }
        }

        if (completedNaturally)
            process.Complete();
    }

    private static string NormalizeRecordToken(string token) =>
        token.Length > 1 && token[0] == '\n' && token[1] == '\u001e'
            ? token[1..]
            : token;
}

internal sealed class LibGit2CommitReader : IGitCommitReader
{
    public string Backend => "libgit2";

    public void Read(
        string repositoryPath,
        GitProjection projection,
        string? directSha,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken,
        Func<GitCommitRecord, bool> onCommit)
    {
        ArgumentNullException.ThrowIfNull(createRepository);
        ArgumentNullException.ThrowIfNull(onCommit);
        using var repository = createRepository(repositoryPath);
        var metadata = GitCommitMetadataPlan.Create(projection);
        var commits = string.IsNullOrWhiteSpace(directSha)
            ? repository.Commits.QueryBy(new CommitFilter { SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time })
            : new[] { repository.Lookup<Commit>(directSha) }.Where(static commit => commit is not null)!;

        foreach (var commit in commits)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!onCommit(metadata.Create(repository.Info.Path, commit!)))
                break;
        }
    }
}

/// <summary>Projection-shaped NUL protocol used by both commit-reader backends.</summary>
internal readonly record struct GitCommitMetadataPlan(
    bool IncludesMessage,
    bool IncludesMessageShort,
    bool IncludesAuthor,
    bool IncludesAuthorEmail,
    bool IncludesCommitter,
    bool IncludesCommitterEmail,
    bool IncludesCommittedWhen)
{
    public string GitFormat
    {
        get
        {
            var format = new StringBuilder("tformat:%x1e%H%x00");
            if (IncludesMessage)
                format.Append("%B%x00");
            if (IncludesMessageShort)
                format.Append("%s%x00");
            if (IncludesAuthor)
                format.Append("%an%x00");
            if (IncludesAuthorEmail)
                format.Append("%ae%x00");
            if (IncludesCommitter)
                format.Append("%cn%x00");
            if (IncludesCommitterEmail)
                format.Append("%ce%x00");
            if (IncludesCommittedWhen)
                format.Append("%cI%x00");
            return format.ToString();
        }
    }

    public static GitCommitMetadataPlan Create(GitProjection projection) => new(
        projection.Includes("Message"),
        projection.Includes("MessageShort"),
        projection.Includes("Author"),
        projection.Includes("AuthorEmail"),
        projection.Includes("Committer"),
        projection.Includes("CommitterEmail"),
        projection.Includes("CommittedWhen"));

    public GitCommitRecord Read(string repositoryPath, string header, GitNulDelimitedUtf8Reader reader) => new(
        repositoryPath,
        header[1..],
        IncludesMessage ? ReadRequired(reader, "commit message") : null,
        IncludesMessageShort ? ReadRequired(reader, "commit summary") : null,
        IncludesAuthor ? ReadRequired(reader, "author") : null,
        IncludesAuthorEmail ? ReadRequired(reader, "author email") : null,
        IncludesCommitter ? ReadRequired(reader, "committer") : null,
        IncludesCommitterEmail ? ReadRequired(reader, "committer email") : null,
        IncludesCommittedWhen ? ReadCommittedWhen(ReadRequired(reader, "commit timestamp")) : default);

    public GitCommitRecord Create(string repositoryPath, Commit commit) => new(
        repositoryPath,
        commit.Sha,
        IncludesMessage ? commit.Message : null,
        IncludesMessageShort ? commit.MessageShort : null,
        IncludesAuthor ? commit.Author.Name : null,
        IncludesAuthorEmail ? commit.Author.Email : null,
        IncludesCommitter ? commit.Committer.Name : null,
        IncludesCommitterEmail ? commit.Committer.Email : null,
        IncludesCommittedWhen ? commit.Committer.When : default);

    private static string ReadRequired(GitNulDelimitedUtf8Reader reader, string field) =>
        reader.ReadToken() ?? throw new InvalidDataException($"Git log ended while reading the {field}.");

    private static DateTimeOffset ReadCommittedWhen(string value)
    {
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var committedWhen))
            throw new InvalidDataException($"Git emitted an invalid ISO-8601 commit timestamp '{value}'.");
        return committedWhen;
    }
}

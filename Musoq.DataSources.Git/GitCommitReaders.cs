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
    DateTimeOffset CommittedWhen,
    string[]? ParentShas = null);

/// <summary>Backend selection is deliberately per operation so reference readers can remain streaming and independently testable.</summary>
internal static class GitOperationReaders
{
    public static IGitCommitReader LibGit2Commits { get; } = new LibGit2CommitReader();
    public static IGitCommitReader CliCommits { get; } = new GitCliCommitReader();
    public static IGitCommitReader Commits { get; } = LibGit2Commits;

    // Local tags and stashes prefer the streaming CLI readers; LibGit2 remains an explicit compatibility fallback.
    public static IGitBranchReader Branches { get; } = new LibGit2BranchReader();
    public static IGitBranchReader CliBranches { get; } = new GitCliBranchReader();
    public static IGitTagReader Tags { get; } = new LibGit2TagReader();
    public static IGitTagReader CliTags { get; } = new GitCliTagReader();
    public static IGitStashReader Stashes { get; } = new LibGit2StashReader();
    public static IGitStashReader CliStashes { get; } = new GitCliStashReader();
    public static IGitRemoteReader Remotes { get; } = new LibGit2RemoteReader();
    public static IGitRemoteReader CliRemotes { get; } = new GitCliRemoteReader();
    public static IGitRemoteTagReader RemoteTags { get; } = new GitCliRemoteTagReader();
    public static IGitStatusReader Status { get; } = new LibGit2StatusReader();
    public static IGitStatusReader CliStatus { get; } = new GitCliStatusReader();
}

internal sealed class GitCliCommitReader : IGitCommitReader
{
    private readonly IGitCliProcessFactory _factory;
    private readonly GitHistoryBackendOptions _options;
    internal GitCliCommitReader(IGitCliProcessFactory? factory = null, GitHistoryBackendOptions? options = null)
    {
        _factory = factory ?? GitCliProcessFactory.Default;
        _options = options ?? GitHistoryBackendOptions.Default;
    }
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
        var native = projection.NativePatterns is null ? null : new GitCommitPatternReader(projection);
        var arguments = new List<string>
        {
            "log",
            "--encoding=UTF-8",
            "--date-order",
            "--no-ext-diff",
            "-z",
            "--format=" + (native?.Format ?? metadata.GitFormat)
        };

        if (!string.IsNullOrWhiteSpace(directSha))
        {
            // An equality predicate on SHA is a direct object lookup, not a history scan. The evaluator predicate
            // remains authoritative, so an invalid/ambiguous object simply follows Git's normal actionable error.
            arguments.Add("--max-count=1");
            arguments.Add(directSha);
        }

        using var process = _factory.Start(new GitCliProcessRequest(repositoryPath, _options.Executable,
            GitHistoryBackendOptions.BackendSettingName, arguments, GitCliProcessEnvironment.Default), cancellationToken);
        using var reader = new GitNulDelimitedUtf8Reader(process.StandardOutput);
        using var decoding = new GitDecodeAccounting(reader, projection.NativePatterns);
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

            GitCommitRecord record = default;
            if (native is not null && !native.Read(repositoryPath, header, reader, cancellationToken, out record)) continue;
            if (native is null) record = metadata.Read(repositoryPath, header, reader);
            if (!onCommit(record))
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
        var native = projection.NativePatterns is null ? null : new GitCommitPatternReader(projection);
        var commits = string.IsNullOrWhiteSpace(directSha)
            ? repository.Commits.QueryBy(new CommitFilter { SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time })
            : new[] { repository.Lookup<Commit>(directSha) }.Where(static commit => commit is not null)!;

        foreach (var commit in commits)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GitCommitRecord record = default;
            if (native is not null && !native.Read(repository.Info.Path, commit!, out record)) continue;
            if (native is null) record = metadata.Create(repository.Info.Path, commit!);
            if (metadata.IncludesParents)
                record = record with { ParentShas = commit!.Parents.Select(parent => parent.Sha).ToArray() };
            if (!onCommit(record))
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
    bool IncludesCommittedWhen,
    bool IncludesParents)
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
            if (IncludesParents)
                format.Append("%P%x00");
            return format.ToString();
        }
    }

    public static GitCommitMetadataPlan Create(GitProjection projection) => projection.Includes("Self")
        ? new(true, true, true, true, true, true, true, true) : new(
        projection.Includes("Message"),
        projection.Includes("MessageShort"),
        projection.Includes("Author"),
        projection.Includes("AuthorEmail"),
        projection.Includes("Committer"),
        projection.Includes("CommitterEmail"),
        projection.Includes("CommittedWhen"),
        projection.Includes("Parents"));

    public GitCommitRecord Read(string repositoryPath, string header, GitNulDelimitedUtf8Reader reader) => new(
        repositoryPath,
        header[1..],
        IncludesMessage ? ReadRequired(reader, "commit message") : null,
        IncludesMessageShort ? ReadRequired(reader, "commit summary") : null,
        IncludesAuthor ? ReadRequired(reader, "author") : null,
        IncludesAuthorEmail ? ReadRequired(reader, "author email") : null,
        IncludesCommitter ? ReadRequired(reader, "committer") : null,
        IncludesCommitterEmail ? ReadRequired(reader, "committer email") : null,
        IncludesCommittedWhen ? ReadCommittedWhen(ReadRequired(reader, "commit timestamp")) : default,
        IncludesParents ? ReadRequired(reader, "commit parents").Split(' ', System.StringSplitOptions.RemoveEmptyEntries) : null);

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

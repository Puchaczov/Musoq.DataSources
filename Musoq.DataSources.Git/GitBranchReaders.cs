using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using LibGit2Sharp;

namespace Musoq.DataSources.Git;

internal interface IGitBranchReader
{
    string Backend { get; }

    void Read(
        string repositoryPath,
        GitProjection projection,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken,
        Func<GitBranchRecord, bool> onBranch);
}

internal readonly record struct GitBranchRecord(
    string RepositoryPath,
    string FriendlyName,
    string CanonicalName,
    bool IsRemote,
    bool IsTracking,
    bool IsCurrentRepositoryHead,
    string? TrackedBranchCanonicalName,
    int? AheadBy,
    int? BehindBy,
    string? TipSha,
    string? UpstreamBranchCanonicalName,
    string? RemoteName)
{
    public static GitBranchRecord Create(string repositoryPath, Branch branch, GitProjection projection)
    {
        // Runtime-v2 may report only a downstream nested leaf (Tip.Sha), not the intermediate branch property.
        // Preserve these compact identities until the engine exposes that dependency explicitly.
        var needsTracking = true;
        var tracking = needsTracking ? branch.TrackingDetails : null;
        return new(
        repositoryPath,
        branch.FriendlyName,
        branch.CanonicalName,
        branch.IsRemote,
        needsTracking && branch.IsTracking,
        branch.IsCurrentRepositoryHead,
        needsTracking ? branch.TrackedBranch?.CanonicalName : null,
        tracking?.AheadBy,
        tracking?.BehindBy,
        branch.Tip?.Sha,
        needsTracking ? branch.UpstreamBranchCanonicalName : null,
        needsTracking ? branch.RemoteName : null);
    }
}

internal sealed class LibGit2BranchReader : IGitBranchReader
{
    public string Backend => "libgit2";

    public void Read(
        string repositoryPath,
        GitProjection projection,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken,
        Func<GitBranchRecord, bool> onBranch)
    {
        using var repository = createRepository(repositoryPath);
        foreach (var branch in repository.Branches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!onBranch(GitBranchRecord.Create(repository.Info.Path, branch, projection)))
                break;
        }
    }
}

/// <summary>
/// Safe CLI candidate for refs. It is retained for parity/benchmark qualification; branch selection remains LibGit2
/// until a corpus shows the required twenty-percent improvement without a semantic difference.
/// </summary>
internal sealed class GitCliBranchReader : IGitBranchReader
{
    public string Backend => "git-cli";

    public void Read(
        string repositoryPath,
        GitProjection projection,
        Func<string, Repository> createRepository,
        CancellationToken cancellationToken,
        Func<GitBranchRecord, bool> onBranch)
    {
        var arguments = new[]
        {
            "for-each-ref",
            "--format=%1e%(refname)%00%(refname:short)%00%(objectname)%00%(upstream)%00%(ahead-behind:HEAD)%00%(HEAD)%00",
            "refs/heads",
            "refs/remotes"
        };
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
                throw new InvalidDataException("Git branch protocol lost its record delimiter.");

            var canonicalName = header[1..];
            var friendlyName = ReadRequired(reader, "branch friendly name");
            var tipSha = ReadRequired(reader, "branch target");
            var upstream = ReadRequired(reader, "branch upstream");
            var tracking = ReadRequired(reader, "branch ahead/behind state");
            var head = ReadRequired(reader, "HEAD marker");
            var isRemote = canonicalName.StartsWith("refs/remotes/", StringComparison.Ordinal);
            var isTracking = !string.IsNullOrWhiteSpace(upstream);
            var (aheadBy, behindBy) = ParseTracking(tracking);
            var remoteName = RemoteName(canonicalName, upstream, isRemote);
            var record = new GitBranchRecord(
                repositoryPath,
                friendlyName,
                canonicalName,
                isRemote,
                isTracking,
                head == "*",
                isTracking ? upstream : null,
                aheadBy,
                behindBy,
                string.IsNullOrWhiteSpace(tipSha) ? null : tipSha,
                isTracking ? upstream : null,
                remoteName);
            if (!onBranch(record))
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
        token.TrimStart('\r', '\n');

    private static string ReadRequired(GitNulDelimitedUtf8Reader reader, string field) =>
        reader.ReadToken() ?? throw new InvalidDataException($"Git branch output ended while reading the {field}.");

    private static (int? AheadBy, int? BehindBy) ParseTracking(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && int.TryParse(parts[0], out var ahead) && int.TryParse(parts[1], out var behind)
            ? (ahead, behind)
            : (null, null);
    }

    private static string? RemoteName(string canonicalName, string upstream, bool isRemote)
    {
        var remoteReference = isRemote ? canonicalName : upstream;
        const string prefix = "refs/remotes/";
        if (!remoteReference.StartsWith(prefix, StringComparison.Ordinal))
            return null;
        var remainder = remoteReference[prefix.Length..];
        var separator = remainder.IndexOf('/');
        return separator > 0 ? remainder[..separator] : null;
    }
}

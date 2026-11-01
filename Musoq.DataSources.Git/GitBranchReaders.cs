using System;
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
    string? RemoteName,
    bool TrackingLoaded = true)
{
    public static GitBranchRecord Create(string repositoryPath, Branch branch, GitProjection projection)
    {
        // Runtime-v2 may report only a downstream nested leaf (Tip.Sha), not the intermediate branch property.
        // Preserve these compact identities until the engine exposes that dependency explicitly.
        var needsTracking = projection.Includes("BranchTrackingDetails") || projection.Includes("Self");
        if (needsTracking) projection.NativePatterns?.RichFieldLoaded();
        var tracking = needsTracking ? branch.TrackingDetails : null;
        return new(
        repositoryPath,
        branch.FriendlyName,
        branch.CanonicalName,
        branch.IsRemote,
        branch.IsTracking,
        branch.IsCurrentRepositoryHead,
        branch.TrackedBranch?.CanonicalName,
        tracking?.AheadBy,
        tracking?.BehindBy,
        branch.Tip?.Sha,
        branch.UpstreamBranchCanonicalName,
        branch.RemoteName,
        needsTracking);
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
            if (projection.NativePatterns?.Matches(branch, GitNativePatterns.Branches) == false) continue;
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
    private readonly GitHistoryBackendOptions _options;
    public GitCliBranchReader(GitHistoryBackendOptions? options = null) => _options = options ?? GitHistoryBackendOptions.Default;
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
            projection.NativePatterns is null
                ? "--format=%1e%(refname)%00%(refname:short)%00%(objectname)%00%(upstream)%00%(upstream:track)%00%(HEAD)%00"
                : "--format=%1e%(refname)%00%(refname:short)%00%(upstream)%00%(objectname)%00%(upstream:track)%00%(HEAD)%00",
            "refs/heads",
            "refs/remotes"
        };
        using var process = GitCliProcess.Start(repositoryPath, _options, arguments, cancellationToken);
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
                throw new InvalidDataException("Git branch protocol lost its record delimiter.");

            var canonicalName = header[1..];
            var friendlyName = ReadRequired(reader, "branch friendly name");
            string tipSha, upstream;
            var native = projection.NativePatterns;
            if (native is not null)
            {
                var needsUpstream = native.Requires("UpstreamBranchCanonicalName") || native.Requires("RemoteName");
                upstream = needsUpstream ? ReadRequired(reader, "branch upstream") : string.Empty;
                var partial = new GitBranchRecord(repositoryPath, friendlyName, canonicalName, false, false, false, null, null, null, null,
                    string.IsNullOrWhiteSpace(upstream) ? null : upstream,
                    native.Requires("RemoteName") ? RemoteName(canonicalName, upstream, canonicalName.StartsWith("refs/remotes/", StringComparison.Ordinal)) : null);


                if (!native.Matches(partial, GitNativePatterns.BranchRecords))
                {
                    reader.SkipTokens(needsUpstream ? 3 : 4, cancellationToken);
                    continue;
                }
                if (!needsUpstream) upstream = ReadRequired(reader, "branch upstream");
                tipSha = ReadRequired(reader, "branch target");
            }
            else
            {
                tipSha = ReadRequired(reader, "branch target");
                upstream = ReadRequired(reader, "branch upstream");
            }
            var tracking = ReadRequired(reader, "branch ahead/behind state");
            var head = ReadRequired(reader, "HEAD marker");
            var isRemote = canonicalName.StartsWith("refs/remotes/", StringComparison.Ordinal);
            var isTracking = !string.IsNullOrWhiteSpace(upstream);
            var (aheadBy, behindBy) = isTracking ? ParseTracking(tracking) : ((int?)null, (int?)null);
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

    internal static (int? AheadBy, int? BehindBy) ParseTracking(string value)
    {
        if (value == "[gone]") return (null, null);
        int added = 0, removed = 0;
        foreach (var field in value.Trim('[', ']').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (field.StartsWith("ahead ", StringComparison.Ordinal)) added = int.Parse(field.AsSpan(6));
            else if (field.StartsWith("behind ", StringComparison.Ordinal)) removed = int.Parse(field.AsSpan(7));
            else throw new InvalidDataException("Invalid Git upstream tracking counts.");
        }
        return (added, removed);
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

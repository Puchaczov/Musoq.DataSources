using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;

namespace Musoq.DataSources.Git.Benchmarks;

/// <summary>
/// Direct streaming-reader scenarios for local tags, stashes, and live local-bare remote advertisements.
/// Every operation consumes a rolling checksum and never retains a result collection.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class GitReferenceReaderBenchmarks
{
    private GitReferenceBenchmarkCorpus _corpus = null!;
    private GitReferenceBackendOptions _options = null!;
    private string _stashSha = string.Empty;

    [Params(GitReferenceCorpusProfile.Smoke)]
    public GitReferenceCorpusProfile Profile { get; set; }

    [Params(false, true)]
    public bool Packed { get; set; }

    [Params("git-cli", "libgit2")]
    public string Backend { get; set; } = "git-cli";

    [GlobalSetup]
    public void Setup()
    {
        _corpus = GitReferenceBenchmarkCorpusFactory.Ensure(Profile, Packed);
        _options = GitReferenceBackendOptions.From(new Dictionary<string, string>
        {
            [GitReferenceBackendOptions.BackendSettingName] = Backend
        });

        using var repository = new Repository(_corpus.RepositoryPath);
        _stashSha = repository.Stashes.FirstOrDefault()?.WorkTree?.Sha ?? string.Empty;
    }

    [Benchmark(Baseline = true)]
    public long LocalTagIdentityScan() => ReadTags(
        new GitProjection(true, [nameof(TagEntity.FriendlyName), nameof(TagEntity.CanonicalName)]),
        GitTagReadQuery.Empty);

    [Benchmark]
    public long LocalTagTargetProjection() => ReadTags(
        new GitProjection(true, [nameof(TagEntity.FriendlyName), nameof(TagEntity.CanonicalName), nameof(TagEntity.TargetSha)]),
        GitTagReadQuery.Empty);

    [Benchmark]
    public long LocalTagMetadataProjection() => ReadTags(
        new GitProjection(true, [nameof(TagEntity.FriendlyName), nameof(TagEntity.CanonicalName), nameof(TagEntity.Message), nameof(TagEntity.IsAnnotated)]),
        GitTagReadQuery.Empty);

    [Benchmark]
    public long LocalTagExactFriendlyLookup() => ReadTags(
        new GitProjection(true, [nameof(TagEntity.FriendlyName), nameof(TagEntity.CanonicalName)]),
        new GitTagReadQuery(null, "annotated-0000001", null, null, null, false, null));

    [Benchmark]
    public long LocalTagExactCanonicalLookup() => ReadTags(
        new GitProjection(true, [nameof(TagEntity.FriendlyName), nameof(TagEntity.CanonicalName)]),
        new GitTagReadQuery("refs/tags/annotated-0000001", null, null, null, null, false, null));

    [Benchmark]
    public long LocalTagFiniteExactNameLookup() => ReadTags(
        new GitProjection(true, [nameof(TagEntity.FriendlyName), nameof(TagEntity.CanonicalName)]),
        new GitTagReadQuery(null, null, null, ["annotated-0000001", "lightweight-0000010"], null, false, null));

    [Benchmark]
    public long LocalTagCursorScan() => ReadTags(
        new GitProjection(true, [nameof(TagEntity.FriendlyName), nameof(TagEntity.CanonicalName)]),
        new GitTagReadQuery(null, null, null, null, "refs/tags/annotated-0000010", false, null));

    [Benchmark]
    public long LocalTagEarlyTake() => ReadTagsWithLimit(
        new GitProjection(true, [nameof(TagEntity.FriendlyName), nameof(TagEntity.CanonicalName)]),
        GitTagReadQuery.Empty,
        32);

    [Benchmark]
    public long StashIdentityScan() => ReadStashes(
        new GitProjection(true, [nameof(StashEntity.Selector), nameof(StashEntity.Sha)]),
        GitStashReadQuery.Empty);

    [Benchmark]
    public long StashParentProjection() => ReadStashes(
        new GitProjection(true, [nameof(StashEntity.Selector), nameof(StashEntity.Sha), nameof(StashEntity.Index),
            nameof(StashEntity.WorkTree), nameof(StashEntity.UntrackedFiles)]),
        GitStashReadQuery.Empty);

    [Benchmark]
    public long StashExactSelectorLookup() => ReadStashes(
        new GitProjection(true, [nameof(StashEntity.Selector), nameof(StashEntity.Sha)]),
        new GitStashReadQuery("stash@{0}", null, null, null));

    [Benchmark]
    public long StashShaFilter() => ReadStashes(
        new GitProjection(true, [nameof(StashEntity.Selector), nameof(StashEntity.Sha)]),
        new GitStashReadQuery(null, _stashSha, null, null));

    [Benchmark]
    public long StashEarlyTake() => ReadStashesWithLimit(
        new GitProjection(true, [nameof(StashEntity.Selector), nameof(StashEntity.Sha)]),
        GitStashReadQuery.Empty,
        4);

    [Benchmark]
    public long RemoteTagLeanAdvertisement() => ReadRemoteTags(
        new GitProjection(true, [nameof(RemoteTagEntity.FriendlyName), nameof(RemoteTagEntity.CanonicalName), nameof(RemoteTagEntity.ObjectSha)]),
        GitRemoteTagReadQuery.Empty);

    [Benchmark]
    public long RemoteTagPeelAdvertisement() => ReadRemoteTags(
        new GitProjection(true, [nameof(RemoteTagEntity.FriendlyName), nameof(RemoteTagEntity.CanonicalName),
            nameof(RemoteTagEntity.ObjectSha), nameof(RemoteTagEntity.PeeledSha), nameof(RemoteTagEntity.IsAnnotated)]),
        GitRemoteTagReadQuery.Empty);

    [Benchmark]
    public long RemoteTagExactPattern() => ReadRemoteTags(
        new GitProjection(true, [nameof(RemoteTagEntity.FriendlyName), nameof(RemoteTagEntity.CanonicalName), nameof(RemoteTagEntity.ObjectSha)]),
        new GitRemoteTagReadQuery(null, "annotated-0000001", null, null, null, false, null));

    [Benchmark]
    public long RemoteTagExactCanonicalPattern() => ReadRemoteTags(
        new GitProjection(true, [nameof(RemoteTagEntity.FriendlyName), nameof(RemoteTagEntity.CanonicalName), nameof(RemoteTagEntity.ObjectSha)]),
        new GitRemoteTagReadQuery("refs/tags/annotated-0000001", null, null, null, null, false, null));

    [Benchmark]
    public long RemoteTagFinitePatternLookup() => ReadRemoteTags(
        new GitProjection(true, [nameof(RemoteTagEntity.FriendlyName), nameof(RemoteTagEntity.CanonicalName), nameof(RemoteTagEntity.ObjectSha)]),
        new GitRemoteTagReadQuery(null, null, null, ["annotated-0000001", "lightweight-0000010"], null, false, null));

    [Benchmark]
    public long RemoteTagCursorFilter() => ReadRemoteTags(
        new GitProjection(true, [nameof(RemoteTagEntity.FriendlyName), nameof(RemoteTagEntity.CanonicalName), nameof(RemoteTagEntity.ObjectSha)]),
        new GitRemoteTagReadQuery(null, null, null, null, "refs/tags/annotated-0000010", false, null));

    [Benchmark]
    public long RemoteTagEarlyTake() => ReadRemoteTagsWithLimit(
        new GitProjection(true, [nameof(RemoteTagEntity.FriendlyName), nameof(RemoteTagEntity.CanonicalName), nameof(RemoteTagEntity.ObjectSha)]),
        GitRemoteTagReadQuery.Empty,
        32);

    [Benchmark]
    public long RemoteTagCapturedAdvertisementReplay()
    {
        long checksum = 17;
        foreach (var tag in GitRemoteTagProtocolParser.Parse(
                     File.ReadLines(_corpus.RemoteAdvertisementPath),
                     _corpus.ClientPath,
                     "origin",
                     _corpus.RemotePath,
                     needsPeel: true,
                     GitRemoteTagReadQuery.Empty,
                     CancellationToken.None))
            checksum = FoldRemote(checksum, tag);
        return checksum;
    }

    private long ReadTags(GitProjection projection, GitTagReadQuery query)
    {
        long checksum = 17;
        foreach (var tag in TagReader.ReadStreaming(
                     _corpus.RepositoryPath,
                     _options,
                     projection,
                     query,
                     static path => new Repository(path),
                     CancellationToken.None))
            checksum = FoldTag(checksum, tag, projection);
        return checksum;
    }

    private long ReadTagsWithLimit(GitProjection projection, GitTagReadQuery query, int take)
    {
        long checksum = 17;
        var count = 0;
        foreach (var tag in TagReader.ReadStreaming(
                     _corpus.RepositoryPath,
                     _options,
                     projection,
                     query,
                     static path => new Repository(path),
                     CancellationToken.None))
        {
            checksum = FoldTag(checksum, tag, projection);
            if (++count == take)
                break;
        }
        return checksum;
    }

    private long ReadStashes(GitProjection projection, GitStashReadQuery query)
    {
        long checksum = 17;
        foreach (var stash in StashReader.ReadStreaming(
                     _corpus.RepositoryPath,
                     _options,
                     projection,
                     query,
                     static path => new Repository(path),
                     CancellationToken.None))
            checksum = FoldStash(checksum, stash, projection);
        return checksum;
    }

    private long ReadStashesWithLimit(GitProjection projection, GitStashReadQuery query, int take)
    {
        long checksum = 17;
        var count = 0;
        foreach (var stash in StashReader.ReadStreaming(
                     _corpus.RepositoryPath,
                     _options,
                     projection,
                     query,
                     static path => new Repository(path),
                     CancellationToken.None))
        {
            checksum = FoldStash(checksum, stash, projection);
            if (++count == take)
                break;
        }
        return checksum;
    }

    private long ReadRemoteTags(GitProjection projection, GitRemoteTagReadQuery query)
    {
        long checksum = 17;
        GitOperationReaders.RemoteTags.Read(
            _corpus.ClientPath,
            "origin",
            GitReferenceBackendOptions.Default,
            projection,
            query,
            static path => new Repository(path),
            CancellationToken.None,
            tag =>
            {
                checksum = FoldRemote(checksum, tag);
                return true;
            });
        return checksum;
    }

    private long ReadRemoteTagsWithLimit(GitProjection projection, GitRemoteTagReadQuery query, int take)
    {
        long checksum = 17;
        var count = 0;
        GitOperationReaders.RemoteTags.Read(
            _corpus.ClientPath,
            "origin",
            GitReferenceBackendOptions.Default,
            projection,
            query,
            static path => new Repository(path),
            CancellationToken.None,
            tag =>
            {
                checksum = FoldRemote(checksum, tag);
                return ++count < take;
            });
        return checksum;
    }

    private IGitTagReader TagReader => Backend == "libgit2" ? GitOperationReaders.Tags : GitOperationReaders.CliTags;

    private IGitStashReader StashReader => Backend == "libgit2" ? GitOperationReaders.Stashes : GitOperationReaders.CliStashes;

    private static long FoldTag(long checksum, GitTagRecord tag, GitProjection projection) =>
        GitFileHistoryBenchmarks.Fold(
            checksum,
            tag.FriendlyName,
            tag.CanonicalName,
            projection.Includes(nameof(TagEntity.TargetSha)) ? tag.TargetSha : null,
            projection.Includes(nameof(TagEntity.Message)) ? tag.Message : null,
            projection.Includes(nameof(TagEntity.IsAnnotated)) ? tag.IsAnnotated.ToString() : null);

    private static long FoldStash(long checksum, GitStashRecord stash, GitProjection projection) =>
        GitFileHistoryBenchmarks.Fold(
            checksum,
            stash.Selector,
            stash.Sha,
            projection.Includes(nameof(StashEntity.Message)) ? stash.Message : null,
            projection.Includes(nameof(StashEntity.Index)) ? stash.IndexSha : null,
            projection.Includes(nameof(StashEntity.WorkTree)) ? stash.WorkTreeSha : null,
            projection.Includes(nameof(StashEntity.UntrackedFiles)) ? stash.UntrackedFilesSha : null);

    private static long FoldRemote(long checksum, GitRemoteTagRecord tag) =>
        GitFileHistoryBenchmarks.Fold(checksum, tag.FriendlyName, tag.CanonicalName, tag.ObjectSha, tag.PeeledSha,
            tag.IsAnnotated.ToString());
}

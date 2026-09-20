using BenchmarkDotNet.Attributes;
using LibGit2Sharp;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Evaluator.Tables;
using Musoq.Schema;

namespace Musoq.DataSources.Git.Benchmarks;

/// <summary>
/// End-to-end compiled Musoq SQL scenarios for reference sources. Result-producing cases use bounded windows so
/// evaluator result materialization remains a controlled part of the scenario rather than an unbounded benchmark sink.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class GitReferenceCompiledQueryBenchmarks
{
    private CompiledQuery _tags = null!;
    private CompiledQuery _tagsCount = null!;
    private CompiledQuery _tagsExact = null!;
    private CompiledQuery _tagsFiniteIn = null!;
    private CompiledQuery _tagsCursor = null!;
    private CompiledQuery _tagsSkipTake = null!;
    private CompiledQuery _tagsResidual = null!;
    private CompiledQuery _stashes = null!;
    private CompiledQuery _stashesCount = null!;
    private CompiledQuery _stashExact = null!;
    private CompiledQuery _stashSkipTake = null!;
    private CompiledQuery _remoteTags = null!;
    private CompiledQuery _remoteTagsCount = null!;
    private CompiledQuery _remoteExact = null!;
    private CompiledQuery _remoteCursor = null!;
    private CompiledQuery _remoteSkipTake = null!;
    private CompiledQuery _nestedTags = null!;
    private CompiledQuery _nestedStashes = null!;
    private CompiledQuery _nestedPartial = null!;
    private CompiledQuery _nestedRich = null!;

    [Params(GitReferenceCorpusProfile.Smoke)]
    public GitReferenceCorpusProfile Profile { get; set; }

    [Params(false, true)]
    public bool Packed { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var corpus = GitReferenceBenchmarkCorpusFactory.Ensure(Profile, Packed);
        var repository = Escape(corpus.RepositoryPath);
        var client = Escape(corpus.ClientPath);

        _tags = Compile($"select t.FriendlyName, t.CanonicalName from git.tags('{repository}') t take 32");
        _tagsCount = Compile($"select count(*) from git.tags('{repository}') t");
        _tagsExact = Compile($"select t.FriendlyName, t.CanonicalName from git.tags('{repository}') t where t.FriendlyName = 'annotated-0000001' take 4");
        _tagsFiniteIn = Compile($"select t.FriendlyName from git.tags('{repository}') t where t.FriendlyName in ('annotated-0000001', 'lightweight-0000010') take 8");
        _tagsCursor = Compile($"select t.FriendlyName, t.CanonicalName from git.tags('{repository}') t where t.CanonicalName > 'refs/tags/annotated-0000010' take 32");
        _tagsSkipTake = Compile($"select t.FriendlyName from git.tags('{repository}') t skip 8 take 32");
        _tagsResidual = Compile($"select t.FriendlyName, t.Message from git.tags('{repository}') t where t.Message = 'annotation 1' take 32");

        _stashes = Compile($"select s.Selector, s.Sha from git.stashes('{repository}') s take 8");
        _stashesCount = Compile($"select count(*) from git.stashes('{repository}') s");
        _stashExact = Compile($"select s.Selector, s.Sha from git.stashes('{repository}') s where s.Selector = 'stash@{{0}}' take 4");
        _stashSkipTake = Compile($"select s.Selector from git.stashes('{repository}') s skip 2 take 8");

        _remoteTags = Compile($"select t.FriendlyName, t.CanonicalName, t.ObjectSha from git.remotetags('{client}', 'origin') t take 32");
        _remoteTagsCount = Compile($"select count(*) from git.remotetags('{client}', 'origin') t");
        _remoteExact = Compile($"select t.FriendlyName, t.CanonicalName from git.remotetags('{client}', 'origin') t where t.FriendlyName = 'annotated-0000001' take 4");
        _remoteCursor = Compile($"select t.FriendlyName from git.remotetags('{client}', 'origin') t where t.CanonicalName > 'refs/tags/annotated-0000010' take 32");
        _remoteSkipTake = Compile($"select t.FriendlyName from git.remotetags('{client}', 'origin') t skip 8 take 32");

        _nestedTags = Compile($"select t.FriendlyName, t.CanonicalName from git.repository('{repository}') r cross apply r.Tags as t take 32");
        _nestedStashes = Compile($"select s.Selector, s.Sha from git.repository('{repository}') r cross apply r.Stashes as s take 8");
        _nestedPartial = Compile($"select t.FriendlyName from git.repository('{repository}') r cross apply r.Tags as t take 1");
        _nestedRich = Compile($"select t.TargetSha, t.Message, t.Commit.Sha from git.repository('{repository}') r cross apply r.Tags as t take 32");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var query in Queries())
            query.Dispose();
    }

    [Benchmark]
    public long DirectTags() => Run(_tags);

    [Benchmark]
    public long DirectTagsCount() => Run(_tagsCount);

    [Benchmark]
    public long DirectTagsExactPredicate() => Run(_tagsExact);

    [Benchmark]
    public long DirectTagsFiniteIn() => Run(_tagsFiniteIn);

    [Benchmark]
    public long DirectTagsCursor() => Run(_tagsCursor);

    [Benchmark]
    public long DirectTagsSkipTake() => Run(_tagsSkipTake);

    [Benchmark]
    public long DirectTagsResidualPredicate() => Run(_tagsResidual);

    [Benchmark]
    public long DirectStashes() => Run(_stashes);

    [Benchmark]
    public long DirectStashesCount() => Run(_stashesCount);

    [Benchmark]
    public long DirectStashExactSelector() => Run(_stashExact);

    [Benchmark]
    public long DirectStashesSkipTake() => Run(_stashSkipTake);

    [Benchmark]
    public long DirectRemoteTags() => Run(_remoteTags);

    [Benchmark]
    public long DirectRemoteTagsCount() => Run(_remoteTagsCount);

    [Benchmark]
    public long DirectRemoteTagExactPredicate() => Run(_remoteExact);

    [Benchmark]
    public long DirectRemoteTagCursor() => Run(_remoteCursor);

    [Benchmark]
    public long DirectRemoteTagsSkipTake() => Run(_remoteSkipTake);

    [Benchmark]
    public long NestedTags() => Run(_nestedTags);

    [Benchmark]
    public long NestedStashes() => Run(_nestedStashes);

    [Benchmark]
    public long NestedPartialEnumeration() => Run(_nestedPartial);

    [Benchmark]
    public long NestedRepeatedEnumeration()
    {
        var first = Run(_nestedTags);
        var second = Run(_nestedTags);
        return GitFileHistoryBenchmarks.Fold(first, second.ToString());
    }

    [Benchmark]
    public long NestedRichProperties() => Run(_nestedRich);

    private static long Run(CompiledQuery query)
    {
        using var table = query.Run();
        long checksum = 17;
        foreach (var row in table)
            for (var index = 0; index < row.Count; index++)
                checksum = GitFileHistoryBenchmarks.Fold(checksum, row[index]?.ToString());
        return checksum;
    }

    private IEnumerable<CompiledQuery> Queries()
    {
        yield return _tags;
        yield return _tagsCount;
        yield return _tagsExact;
        yield return _tagsFiniteIn;
        yield return _tagsCursor;
        yield return _tagsSkipTake;
        yield return _tagsResidual;
        yield return _stashes;
        yield return _stashesCount;
        yield return _stashExact;
        yield return _stashSkipTake;
        yield return _remoteTags;
        yield return _remoteTagsCount;
        yield return _remoteExact;
        yield return _remoteCursor;
        yield return _remoteSkipTake;
        yield return _nestedTags;
        yield return _nestedStashes;
        yield return _nestedPartial;
        yield return _nestedRich;
    }

    private static CompiledQuery Compile(string query) => InstanceCreatorHelpers.CompileForExecution(
        query,
        $"GitReferenceBenchmark{Guid.NewGuid():N}",
        new BenchmarkGitSchemaProvider(),
        EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());

    private static string Escape(string path) => path.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "''", StringComparison.Ordinal);

    private sealed class BenchmarkGitSchemaProvider : ISchemaProvider
    {
        public ISchema GetSchema(string schema) => new GitSchema();
    }
}

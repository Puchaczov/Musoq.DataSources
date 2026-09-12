using BenchmarkDotNet.Attributes;
using LibGit2Sharp;
using Musoq.DataSources.Git.Entities;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema.Diagnostics;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git.Benchmarks;

/// <summary>Measures the Git source boundary while consuming only bounded runtime chunks.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class GitReferenceSourceBenchmarks
{
    private GitReferenceBenchmarkCorpus _corpus = null!;

    [Params(GitReferenceCorpusProfile.Smoke)]
    public GitReferenceCorpusProfile Profile { get; set; }

    [Params(false, true)]
    public bool Packed { get; set; }

    [Params("auto", "git-cli", "libgit2")]
    public string Backend { get; set; } = "auto";

    public IReadOnlyDictionary<string, long> LastMetrics { get; private set; } = new Dictionary<string, long>();

    [GlobalSetup]
    public void Setup() => _corpus = GitReferenceBenchmarkCorpusFactory.Ensure(Profile, Packed);

    [Benchmark]
    public long DirectTags() => ConsumeTags(CreateSourcePlan(
        "tags",
        [new SourceColumnRef(nameof(TagEntity.FriendlyName)), new SourceColumnRef(nameof(TagEntity.CanonicalName))]));

    [Benchmark]
    public long DirectTagsCountZeroProjection() => CountTags(CreateSourcePlan("tags", []));

    public long DirectTagsTakeZero() => CountTags(CreateSourcePlan("tags", [], take: 0));

    [Benchmark]
    public long DirectTagsExactPredicate() => ConsumeTags(CreateSourcePlan(
        "tags",
        [new SourceColumnRef(nameof(TagEntity.FriendlyName))],
        Equal(nameof(TagEntity.FriendlyName), "annotated-0000001")));

    [Benchmark]
    public long DirectTagsFiniteInPredicate() => ConsumeTags(CreateSourcePlan(
        "tags",
        [new SourceColumnRef(nameof(TagEntity.FriendlyName))],
        In(nameof(TagEntity.FriendlyName), "annotated-0000001", "lightweight-0000010")));

    [Benchmark]
    public long DirectTagsCursorPredicate() => ConsumeTags(CreateSourcePlan(
        "tags",
        [new SourceColumnRef(nameof(TagEntity.FriendlyName)), new SourceColumnRef(nameof(TagEntity.CanonicalName))],
        GreaterThan(nameof(TagEntity.CanonicalName), "refs/tags/annotated-0000010")));

    [Benchmark]
    public long DirectTagsSkipTake() => ConsumeTags(CreateSourcePlan(
        "tags",
        [new SourceColumnRef(nameof(TagEntity.FriendlyName))],
        skip: 8,
        take: 32));

    [Benchmark]
    public long DirectTagsResidualPredicate() => ConsumeTags(CreateSourcePlan(
        "tags",
        [new SourceColumnRef(nameof(TagEntity.FriendlyName)), new SourceColumnRef(nameof(TagEntity.Message))],
        Equal(nameof(TagEntity.Message), "annotation 1")));

    [Benchmark]
    public long DirectStashes() => ConsumeStashes(CreateSourcePlan(
        "stashes",
        [new SourceColumnRef(nameof(StashEntity.Selector)), new SourceColumnRef(nameof(StashEntity.Sha))]));

    [Benchmark]
    public long DirectStashesCountZeroProjection() => CountStashes(CreateSourcePlan("stashes", []));

    [Benchmark]
    public long DirectStashExactSelector() => ConsumeStashes(CreateSourcePlan(
        "stashes",
        [new SourceColumnRef(nameof(StashEntity.Selector)), new SourceColumnRef(nameof(StashEntity.Sha))],
        Equal(nameof(StashEntity.Selector), "stash@{0}")));

    [Benchmark]
    public long DirectStashesSkipTake() => ConsumeStashes(CreateSourcePlan(
        "stashes",
        [new SourceColumnRef(nameof(StashEntity.Selector))],
        skip: 2,
        take: 8));

    private long ConsumeTags(SourceExecutionPlan plan)
    {
        var diagnostics = new ReferenceBenchmarkDiagnostics();
        var source = new GitSchema().GetRowSource<TagEntity>(
            "tags",
            RuntimeV2TestContexts.CreateExecutionContext(
                allColumns: TagEntity.Columns,
                sourceRuntimeSettings: RuntimeSettings,
                executionPlan: plan,
                sourceDiagnostics: new SourceDiagnostics(diagnostics)),
            _corpus.RepositoryPath);
        long checksum = 17;
        foreach (var chunk in source.Chunks)
        foreach (var tag in chunk)
            checksum = GitFileHistoryBenchmarks.Fold(checksum, tag.FriendlyName, tag.CanonicalName, tag.TargetSha,
                tag.Message, tag.IsAnnotated.ToString());
        SetMetrics(diagnostics);
        return checksum;
    }

    private long CountTags(SourceExecutionPlan plan)
    {
        var diagnostics = new ReferenceBenchmarkDiagnostics();
        var source = new GitSchema().GetRowSource<TagEntity>(
            "tags",
            RuntimeV2TestContexts.CreateExecutionContext(
                allColumns: TagEntity.Columns,
                sourceRuntimeSettings: RuntimeSettings,
                executionPlan: plan,
                sourceDiagnostics: new SourceDiagnostics(diagnostics)),
            _corpus.RepositoryPath);
        long count = 0;
        foreach (var chunk in source.Chunks)
            count += chunk.Count;
        SetMetrics(diagnostics);
        return count;
    }

    private long ConsumeStashes(SourceExecutionPlan plan)
    {
        var diagnostics = new ReferenceBenchmarkDiagnostics();
        var source = new GitSchema().GetRowSource<StashEntity>(
            "stashes",
            RuntimeV2TestContexts.CreateExecutionContext(
                allColumns: StashEntity.Columns,
                sourceRuntimeSettings: RuntimeSettings,
                executionPlan: plan,
                sourceDiagnostics: new SourceDiagnostics(diagnostics)),
            _corpus.RepositoryPath);
        long checksum = 17;
        foreach (var chunk in source.Chunks)
        foreach (var stash in chunk)
            checksum = GitFileHistoryBenchmarks.Fold(checksum, stash.Selector, stash.Sha, stash.Message,
                stash.Index?.Sha, stash.WorkTree?.Sha, stash.UntrackedFiles?.Sha);
        SetMetrics(diagnostics);
        return checksum;
    }

    private long CountStashes(SourceExecutionPlan plan)
    {
        var diagnostics = new ReferenceBenchmarkDiagnostics();
        var source = new GitSchema().GetRowSource<StashEntity>(
            "stashes",
            RuntimeV2TestContexts.CreateExecutionContext(
                allColumns: StashEntity.Columns,
                sourceRuntimeSettings: RuntimeSettings,
                executionPlan: plan,
                sourceDiagnostics: new SourceDiagnostics(diagnostics)),
            _corpus.RepositoryPath);
        long count = 0;
        foreach (var chunk in source.Chunks)
            count += chunk.Count;
        SetMetrics(diagnostics);
        return count;
    }

    private void SetMetrics(ReferenceBenchmarkDiagnostics diagnostics)
    {
        LastMetrics = diagnostics.Snapshot();
        if (LastMetrics.Any(pair => pair.Key.EndsWith("MaximumBufferedRows", StringComparison.Ordinal) && pair.Value > 128))
            throw new InvalidOperationException("Git reference source exceeded its bounded row buffer.");
    }

    private SourceExecutionPlan CreateSourcePlan(
        string table,
        IReadOnlyList<SourceColumnRef> requiredColumns,
        SourcePredicateExpression? predicate = null,
        long? skip = null,
        long? take = null)
    {
        var request = new SourcePlanRequest
        {
            Identity = new SourceIdentity("git", "git", "git", table),
            RequiredColumns = requiredColumns,
            SourceRuntimeSettings = RuntimeSettings,
            Predicate = predicate,
            OrderBy = [],
            Skip = skip,
            Take = take
        };
        return new GitSchema().TryPlanSource(table, request, _corpus.RepositoryPath).ExecutionPlan;
    }

    private IReadOnlyDictionary<string, string> RuntimeSettings => new Dictionary<string, string>
    {
        [GitReferenceBackendOptions.BackendSettingName] = Backend
    };

    private static SourcePredicateExpression Equal(string column, object value) => new SourcePredicateComparison(
        SourcePredicateComparisonOperator.Equal,
        new SourcePredicateColumn(new SourceColumnRef(column)),
        new SourcePredicateLiteral(value));

    private static SourcePredicateExpression GreaterThan(string column, object value) => new SourcePredicateComparison(
        SourcePredicateComparisonOperator.GreaterThan,
        new SourcePredicateColumn(new SourceColumnRef(column)),
        new SourcePredicateLiteral(value));

    private static SourcePredicateExpression In(string column, params object[] values) => new SourcePredicateIn(
        new SourcePredicateColumn(new SourceColumnRef(column)),
        values.Select(static value => new SourcePredicateLiteral(value)).ToArray());
}

/// <summary>Measures remote-tag source execution against the local bare remote only.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class GitRemoteTagSourceBenchmarks
{
    private GitReferenceBenchmarkCorpus _corpus = null!;

    [Params(GitReferenceCorpusProfile.Smoke)]
    public GitReferenceCorpusProfile Profile { get; set; }

    [Params(false, true)]
    public bool Packed { get; set; }

    public IReadOnlyDictionary<string, long> LastMetrics { get; private set; } = new Dictionary<string, long>();

    [GlobalSetup]
    public void Setup() => _corpus = GitReferenceBenchmarkCorpusFactory.Ensure(Profile, Packed);

    [Benchmark]
    public long DirectRemoteTags() => Consume(CreateSourcePlan([
        new SourceColumnRef(nameof(RemoteTagEntity.FriendlyName)),
        new SourceColumnRef(nameof(RemoteTagEntity.CanonicalName)),
        new SourceColumnRef(nameof(RemoteTagEntity.ObjectSha)),
        new SourceColumnRef(nameof(RemoteTagEntity.PeeledSha)),
        new SourceColumnRef(nameof(RemoteTagEntity.IsAnnotated))]));

    [Benchmark]
    public long DirectRemoteTagsCountZeroProjection() => Count(CreateSourcePlan([]));

    public long DirectRemoteTagsTakeZero() => Count(CreateSourcePlan([], take: 0));

    [Benchmark]
    public long DirectRemoteTagsExactPredicate() => Consume(CreateSourcePlan(
        [new SourceColumnRef(nameof(RemoteTagEntity.FriendlyName))],
        Equal(nameof(RemoteTagEntity.FriendlyName), "annotated-0000001")));

    [Benchmark]
    public long DirectRemoteTagsCursorPredicate() => Consume(CreateSourcePlan(
        [new SourceColumnRef(nameof(RemoteTagEntity.FriendlyName))],
        GreaterThan(nameof(RemoteTagEntity.CanonicalName), "refs/tags/annotated-0000010")));

    [Benchmark]
    public long DirectRemoteTagsSkipTake() => Consume(CreateSourcePlan(
        [new SourceColumnRef(nameof(RemoteTagEntity.FriendlyName))],
        skip: 8,
        take: 32));

    private long Consume(SourceExecutionPlan plan)
    {
        var diagnostics = new ReferenceBenchmarkDiagnostics();
        var source = new GitSchema().GetRowSource<RemoteTagEntity>(
            "remotetags",
            RuntimeV2TestContexts.CreateExecutionContext(
                allColumns: RemoteTagEntity.Columns,
                sourceRuntimeSettings: new Dictionary<string, string>
                {
                    [GitReferenceBackendOptions.BackendSettingName] = "git-cli"
                },
                executionPlan: plan,
                sourceDiagnostics: new SourceDiagnostics(diagnostics)),
            _corpus.ClientPath,
            "origin");
        long checksum = 17;
        foreach (var chunk in source.Chunks)
        foreach (var tag in chunk)
            checksum = GitFileHistoryBenchmarks.Fold(checksum, tag.FriendlyName, tag.CanonicalName, tag.ObjectSha,
                tag.PeeledSha, tag.IsAnnotated.ToString());
        SetMetrics(diagnostics);
        return checksum;
    }

    private long Count(SourceExecutionPlan plan)
    {
        var diagnostics = new ReferenceBenchmarkDiagnostics();
        var source = new GitSchema().GetRowSource<RemoteTagEntity>(
            "remotetags",
            RuntimeV2TestContexts.CreateExecutionContext(
                allColumns: RemoteTagEntity.Columns,
                sourceRuntimeSettings: new Dictionary<string, string>
                {
                    [GitReferenceBackendOptions.BackendSettingName] = "git-cli"
                },
                executionPlan: plan,
                sourceDiagnostics: new SourceDiagnostics(diagnostics)),
            _corpus.ClientPath,
            "origin");
        long count = 0;
        foreach (var chunk in source.Chunks)
            count += chunk.Count;
        SetMetrics(diagnostics);
        return count;
    }

    private void SetMetrics(ReferenceBenchmarkDiagnostics diagnostics)
    {
        LastMetrics = diagnostics.Snapshot();
        if (LastMetrics.Any(pair => pair.Key.EndsWith("MaximumBufferedRows", StringComparison.Ordinal) && pair.Value > 128))
            throw new InvalidOperationException("Git remote-tag source exceeded its bounded row buffer.");
    }

    private SourceExecutionPlan CreateSourcePlan(
        IReadOnlyList<SourceColumnRef> requiredColumns,
        SourcePredicateExpression? predicate = null,
        long? skip = null,
        long? take = null)
    {
        var request = new SourcePlanRequest
        {
            Identity = new SourceIdentity("git", "git", "git", "remotetags"),
            RequiredColumns = requiredColumns,
            SourceRuntimeSettings = new Dictionary<string, string>
            {
                [GitReferenceBackendOptions.BackendSettingName] = "git-cli"
            },
            Predicate = predicate,
            OrderBy = [],
            Skip = skip,
            Take = take
        };
        return new GitSchema().TryPlanSource("remotetags", request, _corpus.ClientPath, "origin").ExecutionPlan;
    }

    private static SourcePredicateExpression Equal(string column, object value) => new SourcePredicateComparison(
        SourcePredicateComparisonOperator.Equal,
        new SourcePredicateColumn(new SourceColumnRef(column)),
        new SourcePredicateLiteral(value));

    private static SourcePredicateExpression GreaterThan(string column, object value) => new SourcePredicateComparison(
        SourcePredicateComparisonOperator.GreaterThan,
        new SourcePredicateColumn(new SourceColumnRef(column)),
        new SourcePredicateLiteral(value));
}

#nullable enable
using Musoq.DataSources.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.GitHub;

internal interface IFilteredGitHubApi
{
    IGitHubApi WithPatterns(NativePatternExecution patterns, CancellationToken cancellationToken);
}

internal static class GitHubPatternApi
{
    internal static RowSource<T> Read<T>(SourceExecutionContext context, IGitHubApi? api, string source, Func<SourceExecutionContext, RowSource<T>> read)
        => PatternSourcePlan.Read(context, read, api is null or IFilteredGitHubApi ? GitHubPatternColumns.Get(source)?.Names : null,
            nativeOwnsWindow: true);
    internal static IGitHubApi Bind(IGitHubApi api, SourceExecutionContext context, CancellationToken token)
        => NativePatternExecution.Get(context) is { HasPredicates: true } native && api is IFilteredGitHubApi filtered
            ? filtered.WithPatterns(native, token) : api;
}

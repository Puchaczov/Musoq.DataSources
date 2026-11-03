#nullable enable
using Musoq.DataSources.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;
namespace Musoq.DataSources.Jira;
internal interface IFilteredJiraApi
{
    IJiraApi WithPatterns(NativePatternExecution patterns, CancellationToken cancellationToken);
}
internal static class JiraPatternApi
{
    internal static RowSource<T> Read<T>(SourceExecutionContext context, IJiraApi? api, string source, Func<SourceExecutionContext, RowSource<T>> read)
        => PatternSourcePlan.Read(context, read, api is null or IFilteredJiraApi ? JiraPatternColumns.Get(source)?.Names : null, nativeOwnsWindow: source == "issues");
    internal static IJiraApi Bind(IJiraApi api, SourceExecutionContext context, CancellationToken token)
    {
        api = api is JiraApi concrete ? concrete.WithReporting(context.WarningReporter, token) : api;
        return NativePatternExecution.Get(context) is { HasPredicates: true } native && api is IFilteredJiraApi filtered
            ? filtered.WithPatterns(native, token) : api;
    }
}

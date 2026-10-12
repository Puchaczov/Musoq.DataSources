#nullable enable
using Musoq.DataSources.Common;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Search.Components.Planning;

internal static class TypedPredicateFixture
{
    internal static bool Matches<TRow>(SourcePredicateExpression? predicate, TRow row)
    {
        var plan = new TypedPredicatePlan();
        plan.Bind<TRow>(predicate);
        return plan.Get<TRow>()?.Invoke(row) ?? true;
    }
}

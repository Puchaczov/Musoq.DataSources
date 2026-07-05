using System.Collections.Generic;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesReadPlan
{
    public const string PropertyName = "SeparatedValuesReadPlan";

    public static readonly SeparatedValuesReadPlan Empty = new()
    {
        ProjectionAccepted = false,
        HasResidualWork = false
    };

    public bool ProjectionAccepted { get; init; }

    public bool HasResidualWork { get; init; }

    public SourcePredicateExpression? AcceptedPredicate { get; init; }

    public static SeparatedValuesReadPlan From(SourceExecutionPlan plan)
    {
        if (plan.Properties is not null &&
            plan.Properties.TryGetValue(PropertyName, out var value) &&
            value is SeparatedValuesReadPlan readPlan)
            return readPlan;

        return new SeparatedValuesReadPlan
        {
            ProjectionAccepted = plan.AcceptedColumns.Count > 0,
            AcceptedPredicate = plan.AcceptedPredicate,
            HasResidualWork = false
        };
    }

    public static Dictionary<string, object?> CreateProperties(SeparatedValuesReadPlan readPlan)
    {
        return new Dictionary<string, object?>
        {
            [PropertyName] = readPlan
        };
    }
}

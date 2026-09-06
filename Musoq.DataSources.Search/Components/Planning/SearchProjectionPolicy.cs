#nullable enable

using System;
using System.Linq;
using Musoq.Schema.Optimization;
using Musoq.DataSources.Search.Entities;

namespace Musoq.DataSources.Search.Components.Planning;

internal readonly record struct SearchProjectionRequirements(
    bool RetainMatchText,
    bool RetainLineText,
    bool RetainCaptures,
    bool RetainContext,
    bool RetainMatchedBytes,
    bool RetainWindowBytes)
{
    public static SearchProjectionRequirements From(SourceExecutionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(executionContext);

        // The generated source context carries the complete inferred schema in
        // AllColumns. AcceptedColumns is the authoritative required-field set
        // when Core has planned one, including fields used only by a residual
        // predicate or order expression. Direct source callers historically
        // provide their projection through AllColumns, so retain that fallback.
        var columnNames = executionContext.Plan.AcceptedColumns.Count > 0
            ? executionContext.Plan.AcceptedColumns
                .Select(static column => SearchPredicatePlanning.NormalizeColumnName(column.Name))
                .ToArray()
            : executionContext.AllColumns
                .Select(static column => column.ColumnName)
                .ToArray();

        // An empty runtime column set means that the host did not provide a
        // projection, so preserve complete typed-row compatibility.
        if (columnNames.Length == 0)
            return new(
                RetainMatchText: true,
                RetainLineText: true,
                RetainCaptures: true,
                RetainContext: true,
                RetainMatchedBytes: true,
                RetainWindowBytes: true);

        var retainMatchText = false;
        var retainLineText = false;
        var retainCaptures = false;
        var retainContext = false;
        var retainMatchedBytes = false;
        var retainWindowBytes = false;
        foreach (var columnName in columnNames)
        {
            if (string.Equals(columnName, nameof(SearchMatch.MatchText), StringComparison.OrdinalIgnoreCase))
                retainMatchText = true;
            if (string.Equals(columnName, nameof(SearchLine.LineText), StringComparison.OrdinalIgnoreCase))
                retainLineText = true;
            if (string.Equals(columnName, nameof(SearchMatch.Captures), StringComparison.OrdinalIgnoreCase))
                retainCaptures = true;
            if (string.Equals(columnName, nameof(SearchMatch.Context), StringComparison.OrdinalIgnoreCase))
                retainContext = true;
            if (string.Equals(columnName, nameof(SearchByteMatch.MatchedBytes), StringComparison.OrdinalIgnoreCase))
                retainMatchedBytes = true;
            if (string.Equals(columnName, nameof(SearchByteMatch.WindowBytes), StringComparison.OrdinalIgnoreCase))
                retainWindowBytes = true;
        }

        return new(
            retainMatchText,
            retainLineText,
            retainCaptures,
            retainContext,
            retainMatchedBytes,
            retainWindowBytes);
    }
}

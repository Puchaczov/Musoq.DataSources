using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json;

internal sealed class JsonRowLayout
{
    private readonly BoundColumn[] _columns;
    private readonly int _outputCount;
    private readonly Dictionary<string, int> _slotsByName;

    private JsonRowLayout(
        BoundColumn[] columns,
        int outputCount)
    {
        _columns = columns;
        _outputCount = outputCount;
        _slotsByName = new Dictionary<string, int>(columns.Length, StringComparer.Ordinal);
        for (var index = 0; index < columns.Length; index++)
            _slotsByName.Add(columns[index].Name, index);
    }

    public static JsonRowLayout Create(
        StructuredSchemaSnapshot snapshot,
        SourceExecutionContext executionContext)
    {
        var columns = new List<BoundColumn>();
        var outputCount = 0;
        var projectionAccepted = JsonSourcePlanner.IsProjectionAccepted(executionContext.Plan) ||
                                 executionContext.Plan.AcceptedColumns.Count > 0;

        if (projectionAccepted)
        {
            var denseFallback = 0;
            foreach (var accepted in executionContext.Plan.AcceptedColumns)
            {
                var name = ResolveName(snapshot, accepted.Name);
                var schemaColumn = FindSchemaColumn(executionContext.AllColumns, name);
                var outputIndex = schemaColumn?.ColumnIndex ?? denseFallback;
                var outputType = schemaColumn?.ColumnType ?? GetSnapshotColumn(snapshot, name).ClrType;
                AddOrUpdate(columns, snapshot, name, outputIndex, outputType);
                outputCount = Math.Max(outputCount, outputIndex + 1);
                denseFallback++;
            }
        }
        else if (executionContext.AllColumns.Count > 0)
        {
            foreach (var schemaColumn in executionContext.AllColumns.OrderBy(column => column.ColumnIndex))
            {
                var name = ResolveName(snapshot, schemaColumn.ColumnName);
                AddOrUpdate(columns, snapshot, name, schemaColumn.ColumnIndex, schemaColumn.ColumnType);
                outputCount = Math.Max(outputCount, schemaColumn.ColumnIndex + 1);
            }
        }
        else
        {
            foreach (var column in snapshot.Columns)
            {
                AddOrUpdate(columns, snapshot, column.Name, column.SourceOrdinal, column.ClrType);
                outputCount++;
            }
        }

        AddPredicateColumns(columns, snapshot, executionContext.Plan.AcceptedPredicate);
        return new JsonRowLayout(columns.ToArray(), outputCount);
    }

    public int GetSlot(string name)
    {
        return _slotsByName[name];
    }

    public bool HasOutputColumns => _outputCount > 0;

    internal QueryRowShape CreateNativeShape()
    {
        var outputs = _columns.Where(static column => column.OutputIndex >= 0)
            .OrderBy(static column => column.OutputIndex).ToArray();
        var fields = outputs.Select((column, slot) =>
        {
            var type = column.OutputType;
            if (type == typeof(object))
                type = typeof(JsonElement);
            if (column.TypeState.IsNullable && type.IsValueType && Nullable.GetUnderlyingType(type) is null)
                type = typeof(Nullable<>).MakeGenericType(type);
            return new QueryRowField(slot, column.SourceOrdinal, column.Name, type,
                column.TypeState.IsNullable || !type.IsValueType || Nullable.GetUnderlyingType(type) is not null);
        }).ToArray();
        return new QueryRowShape(fields);
    }

    private static void AddPredicateColumns(
        List<BoundColumn> columns,
        StructuredSchemaSnapshot snapshot,
        SourcePredicateExpression predicate)
    {
        switch (predicate)
        {
            case null:
                return;
            case SourcePredicateLogical { Operator: SourcePredicateLogicalOperator.And } logical:
                AddPredicateColumns(columns, snapshot, logical.Left);
                AddPredicateColumns(columns, snapshot, logical.Right);
                return;
            case SourcePredicateComparison comparison when
                JsonSourcePlanner.TryGetComparisonParts(comparison, out var name, out _, out _):
                name = ResolveName(snapshot, name);
                AddOrUpdate(columns, snapshot, name, -1, GetSnapshotColumn(snapshot, name).ClrType);
                return;
        }
    }

    private static void AddOrUpdate(
        List<BoundColumn> columns,
        StructuredSchemaSnapshot snapshot,
        string name,
        int outputIndex,
        Type outputType)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (!string.Equals(columns[index].Name, name, StringComparison.Ordinal))
                continue;

            if (outputIndex >= 0)
                columns[index] = columns[index] with { OutputIndex = outputIndex, OutputType = outputType };
            return;
        }

        var column = GetSnapshotColumn(snapshot, name);
        columns.Add(new BoundColumn(
            name,
            column.SourceOrdinal,
            outputIndex,
            outputType,
            column.TypeState));
    }

    private static StructuredColumnSnapshot GetSnapshotColumn(
        StructuredSchemaSnapshot snapshot,
        string name)
    {
        if (snapshot.TryGetColumn(name, out var column))
            return column;
        throw new StructuredUnknownColumnException(name, snapshot.Identity.CanonicalPath);
    }

    private static string ResolveName(StructuredSchemaSnapshot snapshot, string name)
    {
        if (snapshot.TryGetColumn(name, out _))
            return name;

        var dotIndex = name.LastIndexOf('.');
        if (dotIndex >= 0)
        {
            var unqualified = name[(dotIndex + 1)..];
            if (snapshot.TryGetColumn(unqualified, out _))
                return unqualified;
        }

        throw new StructuredUnknownColumnException(name, snapshot.Identity.CanonicalPath);
    }

    private static ISchemaColumn FindSchemaColumn(
        IReadOnlyCollection<ISchemaColumn> columns,
        string name)
    {
        foreach (var column in columns)
            if (string.Equals(column.ColumnName, name, StringComparison.Ordinal) ||
                column.ColumnName.EndsWith('.' + name, StringComparison.Ordinal))
                return column;
        return null;
    }

    private sealed record BoundColumn(
        string Name,
        int SourceOrdinal,
        int OutputIndex,
        Type OutputType,
        StructuredTypeState TypeState);
}

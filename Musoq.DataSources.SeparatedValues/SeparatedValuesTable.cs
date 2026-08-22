#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal sealed class SeparatedValuesTable : ISchemaTable
{
    private readonly ISchemaColumn[] _columns;

    public SeparatedValuesTable(
        StructuredSchemaSnapshot snapshot,
        SourceMetadataContext metadataContext)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(metadataContext);

        var requestedColumns = metadataContext.AllColumns
            .Where(column => snapshot.TryGetColumn(column.ColumnName, out _))
            .ToArray();
        var layout = StructuredExecutionLayout.Bind(
            snapshot,
            requestedColumns.Select(column => column.ColumnName),
            metadataContext.AllColumns.Count == 0);
        var explicitColumns = requestedColumns
            .GroupBy(column => column.ColumnName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        _columns = new ISchemaColumn[layout.Bindings.Length];
        foreach (var binding in layout.Bindings)
        {
            var type = binding.ClrType;
            if (explicitColumns.TryGetValue(binding.Name, out var explicitColumn) &&
                explicitColumn.ColumnType != typeof(object))
                type = explicitColumn.ColumnType;

            _columns[binding.OutputOrdinal] = new SchemaColumn(
                binding.Name,
                binding.OutputOrdinal,
                type,
                explicitColumn?.IntendedTypeName,
                explicitColumn?.ReadModifiers);
        }
    }

    public ISchemaColumn[] Columns => _columns;

    public SchemaTableMetadata Metadata { get; } = new(typeof(object[]));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return _columns.FirstOrDefault(column =>
            string.Equals(column.ColumnName, name, StringComparison.Ordinal));
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return _columns.Where(column =>
                string.Equals(column.ColumnName, name, StringComparison.Ordinal))
            .ToArray();
    }
}

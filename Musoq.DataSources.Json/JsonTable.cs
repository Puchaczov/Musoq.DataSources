#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Musoq.DataSources.Structured;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Managers;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json;

internal sealed class JsonTable : ISchemaTable
{
    private readonly ISchemaColumn[] _columns;

    public JsonTable(
        StructuredSchemaSnapshot snapshot,
        SourceMetadataContext metadataContext)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(metadataContext);

        var requestedColumns = metadataContext.AllColumns;
        var includeCompleteSchema = requestedColumns.Count == 0;
        var layout = StructuredExecutionLayout.Bind(
            snapshot,
            requestedColumns.Select(column => column.ColumnName),
            includeCompleteSchema);
        var explicitTypes = requestedColumns.ToDictionary(
            column => column.ColumnName,
            column => column.ColumnType,
            StringComparer.Ordinal);

        _columns = new ISchemaColumn[layout.Bindings.Length];
        foreach (var binding in layout.Bindings)
        {
            var columnType = binding.ClrType;
            if (explicitTypes.TryGetValue(binding.Name, out var explicitType) && explicitType != typeof(object))
                columnType = explicitType;

            _columns[binding.OutputOrdinal] = new SchemaColumn(
                binding.Name,
                binding.OutputOrdinal,
                columnType);
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

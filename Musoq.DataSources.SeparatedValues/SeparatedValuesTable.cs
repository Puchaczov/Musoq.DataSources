using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.SeparatedValues;

internal class SeparatedValuesTable(string fileName, string separator, bool hasHeader, int skipLines)
    : ISchemaTable
{
    private ISchemaColumn[]? _columns;

    public IReadOnlyCollection<ISchemaColumn>? InferredColumns { get; init; }

    public ISchemaColumn[] Columns
    {
        get
        {
            if (_columns != null)
                return _columns;

            if (InferredColumns is null)
                throw new InvalidOperationException("Inferred columns cannot be null.");

            var file = new FileInfo(fileName);
            var encoding = SeparatedValuesReadModifiers.ResolveFileEncodingOrDefault(InferredColumns);
            var columns = SeparatedValuesHeaderReader.ReadFirstRecord(file, separator, skipLines, 65536, encoding);

            if (columns.Length == 0)
                throw new InvalidOperationException("File is empty.");

            if (hasHeader)
                _columns = columns
                    .Select((header, i) =>
                    {
                        var columnName = SeparatedValuesHelper.MakeHeaderNameValidColumnName(header ?? string.Empty);
                        var inferredColumn = InferredColumns.SingleOrDefault(f => f.ColumnName == columnName);

                        if (inferredColumn == null)
                            return new SchemaColumn(columnName, i, typeof(string));

                        var type = inferredColumn.ColumnType;
                        return type == typeof(object)
                            ? new SchemaColumn(columnName, i, typeof(string), inferredColumn.ReadModifiers)
                            : new SchemaColumn(columnName, i, type, inferredColumn.ReadModifiers);
                    })
                    .Cast<ISchemaColumn>()
                    .ToArray();
            else
                _columns = columns
                    .Select((f, i) =>
                    {
                        var columnName = string.Format(SeparatedValuesHelper.AutoColumnName, i + 1);
                        var inferredColumn = InferredColumns.SingleOrDefault(f => f.ColumnName == columnName);

                        if (inferredColumn == null)
                            return new SchemaColumn(columnName, i, typeof(string));

                        var type = inferredColumn.ColumnType;
                        return type == typeof(object)
                            ? new SchemaColumn(columnName, i, typeof(string), inferredColumn.ReadModifiers)
                            : new SchemaColumn(columnName, i, type, inferredColumn.ReadModifiers);
                    })
                    .Cast<ISchemaColumn>()
                    .ToArray();

            return _columns;
        }
    }

    public SchemaTableMetadata Metadata { get; } = new(typeof(object[]));

    public ISchemaColumn? GetColumnByName(string name)
    {
        return Columns.SingleOrDefault(column => column.ColumnName == name);
    }

    public ISchemaColumn[] GetColumnsByName(string name)
    {
        return Columns.Where(column => column.ColumnName == name).ToArray();
    }
}

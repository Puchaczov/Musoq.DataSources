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
            using var stream = new StreamReader(file.OpenRead());
            var line = string.Empty;

            var currentLine = 0;
            while (!stream.EndOfStream && ((line = stream.ReadLine()) == string.Empty || currentLine < skipLines))
                currentLine += 1;

            if (line is null)
                throw new InvalidOperationException("File is empty.");

            var columns = line.Split([separator], StringSplitOptions.None);

            if (hasHeader)
                _columns = columns
                    .Select((header, i) =>
                    {
                        var type = InferredColumns.SingleOrDefault(f => f.ColumnName == header)?.ColumnType;

                        if (type == null)
                            return new SchemaColumn(SeparatedValuesHelper.MakeHeaderNameValidColumnName(header), i,
                                typeof(string));

                        return type == typeof(object)
                            ? new SchemaColumn(SeparatedValuesHelper.MakeHeaderNameValidColumnName(header), i,
                                typeof(string))
                            : new SchemaColumn(SeparatedValuesHelper.MakeHeaderNameValidColumnName(header), i, type);
                    })
                    .Cast<ISchemaColumn>()
                    .ToArray();
            else
                _columns = columns
                    .Select((f, i) => new SchemaColumn(string.Format(SeparatedValuesHelper.AutoColumnName, i + 1), i,
                        typeof(string)))
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
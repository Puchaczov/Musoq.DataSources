using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal class SeparatedValuesFromStreamRowsSource(
    Stream stream,
    string separator,
    bool hasHeader,
    int skipLines,
    SourceExecutionContext executionContext) : RowSourceBase<object?[]>
{
    private readonly CultureInfo _modifiedCulture = new(CultureInfo.CurrentCulture.Name)
    {
        TextInfo = { ListSeparator = separator }
    };

    protected override void CollectChunks(IChunkWriter<object?[]> writer)
    {
        var columns = GetProjectedColumns(executionContext, out var projectionAccepted);
        var types = columns.ToDictionary(
            col => col.ColumnName,
            col => col.ColumnType.GetUnderlyingNullable());

        var indexToNameMap = executionContext.AllColumns.ToDictionary(
            col => col.ColumnIndex,
            col => col.ColumnName);
        var activeIndexes = GetActiveIndexes(indexToNameMap, columns, projectionAccepted);
        var outputLength = GetOutputLength(columns, projectionAccepted);

        using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024);

        SkipLines(reader, hasHeader ? skipLines + 1 : skipLines);

        using var csvReader = new CsvReader(reader, new CsvConfiguration(_modifiedCulture));
        var chunk = new List<object?[]>();

        while (csvReader.Read())
        {
            writer.CancellationToken.ThrowIfCancellationRequested();

            var rawRow = csvReader.Context.Parser!.Record;

            if (rawRow is null)
                continue;

            chunk.Add(ParseHelpers.ParseRecords(types, rawRow, indexToNameMap, activeIndexes, outputLength));

            if (chunk.Count < RowChunking.DefaultChunkSize)
                continue;

            writer.Write(chunk);
            chunk = [];
        }

        if (chunk.Count > 0)
            writer.Write(chunk);
    }

    private static void SkipLines(TextReader reader, int linesToSkip)
    {
        for (var i = 0; i < linesToSkip; i++)
            reader.ReadLine();
    }

    private static ISchemaColumn[] GetProjectedColumns(
        SourceExecutionContext context,
        out bool projectionAccepted)
    {
        var acceptedColumns = context.Plan.AcceptedColumns;
        projectionAccepted = acceptedColumns.Count > 0;

        if (!projectionAccepted)
            return context.AllColumns.ToArray();

        var acceptedNames = CreateAcceptedColumnNameSet(acceptedColumns, context.AllColumns);

        return context.AllColumns
            .Where(column => acceptedNames.Contains(column.ColumnName))
            .ToArray();
    }

    private static IReadOnlySet<int>? GetActiveIndexes(
        IReadOnlyDictionary<int, string> indexToNameMap,
        IReadOnlyCollection<ISchemaColumn> columns,
        bool projectionAccepted)
    {
        if (!projectionAccepted)
            return null;

        var selectedNames = columns
            .Select(column => column.ColumnName)
            .ToHashSet(StringComparer.Ordinal);

        return indexToNameMap
            .Where(pair => selectedNames.Contains(pair.Value))
            .Select(pair => pair.Key)
            .ToHashSet();
    }

    private static int? GetOutputLength(IReadOnlyCollection<ISchemaColumn> columns, bool projectionAccepted)
    {
        if (!projectionAccepted)
            return null;

        return columns.Count == 0 ? 0 : columns.Max(column => column.ColumnIndex) + 1;
    }

    private static HashSet<string> CreateAcceptedColumnNameSet(
        IReadOnlyCollection<SourceColumnRef> acceptedColumns,
        IReadOnlyCollection<ISchemaColumn> allColumns)
    {
        var allNames = allColumns
            .Select(column => column.ColumnName)
            .ToHashSet(StringComparer.Ordinal);
        var acceptedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var acceptedColumn in acceptedColumns)
        {
            AddIfKnown(acceptedColumn.Name);

            foreach (var part in acceptedColumn.Name.Split('.'))
                AddIfKnown(part);
        }

        return acceptedNames;

        void AddIfKnown(string name)
        {
            if (allNames.Count == 0 || allNames.Contains(name))
                acceptedNames.Add(name);
        }
    }
}

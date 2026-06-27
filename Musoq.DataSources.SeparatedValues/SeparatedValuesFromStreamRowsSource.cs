using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
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
        var types = executionContext.AllColumns.ToDictionary(
            col => col.ColumnName,
            col => col.ColumnType.GetUnderlyingNullable());

        var indexToNameMap = executionContext.AllColumns.ToDictionary(
            col => col.ColumnIndex,
            col => col.ColumnName);

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

            chunk.Add(ParseHelpers.ParseRecords(types, rawRow, indexToNameMap));

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
}

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
using IObjectResolver = Musoq.Schema.DataSources.IObjectResolver;

namespace Musoq.DataSources.SeparatedValues;

internal class SeparatedValuesFromStreamRowsSource(Stream stream, string separator, bool hasHeader, int skipLines, RuntimeContext runtimeContext) : RowSource
{   
    private readonly CultureInfo _modifiedCulture = new(CultureInfo.CurrentCulture.Name)
    {
        TextInfo = { ListSeparator = separator }
    };
    
    public override IEnumerable<IObjectResolver> Rows
    {
        get
        {
            if (runtimeContext is null)
            {
                throw new InvalidOperationException("Runtime context is not set.");
            }
            
            var types = runtimeContext.AllColumns.ToDictionary(
                col => col.ColumnName,
                col => col.ColumnType.GetUnderlyingNullable());

            var nameToIndexMap = runtimeContext.AllColumns.ToDictionary(
                col => col.ColumnName,
                col => col.ColumnIndex);
            var indexToMethodAccessMap = runtimeContext.AllColumns.ToDictionary(
                col => col.ColumnIndex,
                col => new Func<object?[], object?>(objects => objects[col.ColumnIndex]));
            var indexToNameMap = runtimeContext.AllColumns.ToDictionary(
                col => col.ColumnIndex,
                col => col.ColumnName);

            using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024);
            
            SkipLines(reader, hasHeader ? skipLines + 1 : skipLines);
            
            using var csvReader = new CsvReader(reader, new CsvConfiguration(_modifiedCulture));

            while (csvReader.Read())
            {
                if (runtimeContext.EndWorkToken.IsCancellationRequested)
                {
                    yield break;
                }

                var rawRow = csvReader.Context.Parser!.Record;
                
                if (rawRow is null)
                    continue;

                var row = new EntityResolver<object?[]>(ParseHelpers.ParseRecords(types, rawRow, indexToNameMap),
                    nameToIndexMap, indexToMethodAccessMap);
                
                yield return row;
            }
        }
    }

    private static void SkipLines(TextReader reader, int linesToSkip)
    {
        for (var i = 0; i < linesToSkip; i++)
        {
            reader.ReadLine();
        }
    }
}
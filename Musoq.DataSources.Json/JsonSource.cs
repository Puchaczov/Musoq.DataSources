using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Musoq.DataSources.JsonHelpers;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;
using Newtonsoft.Json;

namespace Musoq.DataSources.Json;

/// <summary>
///     Represents a json source.
/// </summary>
public class JsonSource : RowSourceBase<object[]>
{
    private const string JsonSourceName = "json";
    private readonly SourceExecutionContext _executionContext;
    private readonly Stream _stream;

    /// <summary>
    ///     Initializes a new instance of the <see cref="JsonSource" /> class.
    /// </summary>
    /// <param name="stream">Stream with json content.</param>
    /// <param name="executionContext">Execution context.</param>
    public JsonSource(Stream stream, SourceExecutionContext executionContext)
    {
        _stream = stream;
        _executionContext = executionContext;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="JsonSource" /> class.
    /// </summary>
    /// <param name="path">Path to json content.</param>
    /// <param name="executionContext">Execution context.</param>
    public JsonSource(string path, SourceExecutionContext executionContext)
    {
        _executionContext = executionContext;
        _stream = File.OpenRead(path);
    }

    /// <summary>
    ///     Gets the data from json file.
    /// </summary>
    /// <param name="writer">Chunk writer.</param>
    /// <exception cref="NotSupportedException">Thrown when json shape is not supported.</exception>
    protected override void CollectChunks(IChunkWriter<object[]> writer)
    {
        _executionContext.ReportDataSourceBegin(JsonSourceName);
        long totalRowsProcessed = 0;

        try
        {
            using var contentStream = _stream;
            using var contentReader = new StreamReader(contentStream);
            using var reader = new JsonTextReader(contentReader);
            reader.SupportMultipleContent = true;

            if (!reader.Read())
                throw new NotSupportedException("Cannot read file. Json is probably malformed.");

            var rows = reader.TokenType switch
            {
                JsonToken.StartObject => [JsonParser.ParseObject(reader, writer.CancellationToken)],
                JsonToken.StartArray => JsonParser.ParseArray(reader, writer.CancellationToken),
                _ => null
            };

            if (rows == null)
                throw new NotSupportedException("This type of .json file is not supported.");

            var columns = _executionContext.AllColumns
                .OrderBy(column => column.ColumnIndex)
                .ToArray();
            var chunk = new List<object[]>();

            foreach (var row in rows)
            {
                writer.CancellationToken.ThrowIfCancellationRequested();

                var dictionary = (IDictionary<string, object>)row;
                chunk.Add(ProjectRow(dictionary, columns));
                totalRowsProcessed++;

                if (chunk.Count < RowChunking.DefaultChunkSize)
                    continue;

                writer.Write(chunk);
                chunk = [];
            }

            if (chunk.Count > 0)
                writer.Write(chunk);
        }
        finally
        {
            _executionContext.ReportDataSourceEnd(JsonSourceName, totalRowsProcessed);
        }
    }

    private static object[] ProjectRow(
        IDictionary<string, object> row,
        IReadOnlyList<ISchemaColumn> columns)
    {
        if (columns.Count == 0)
            return row.Values.ToArray();

        var values = new object[columns[^1].ColumnIndex + 1];

        foreach (var column in columns)
            if (row.TryGetValue(column.ColumnName, out var value))
                values[column.ColumnIndex] = value;

        return values;
    }
}

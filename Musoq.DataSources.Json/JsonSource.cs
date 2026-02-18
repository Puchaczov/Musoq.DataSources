using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Musoq.DataSources.JsonHelpers;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Newtonsoft.Json;

namespace Musoq.DataSources.Json;

/// <summary>
///     Represents a json source.
/// </summary>
public class JsonSource : RowSourceBase<dynamic>
{
    private const string JsonSourceName = "json";
    private readonly RuntimeContext _runtimeContext;
    private readonly Stream _stream;

    /// <summary>
    ///     Initializes a new instance of the <see cref="JsonSource" /> class.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="runtimeContext"></param>
    public JsonSource(Stream stream, RuntimeContext runtimeContext)
    {
        _stream = stream;
        _runtimeContext = runtimeContext;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="JsonSource" /> class.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="runtimeContext"></param>
    public JsonSource(string path, RuntimeContext runtimeContext)
    {
        _runtimeContext = runtimeContext;
        _stream = File.OpenRead(path);
    }

    /// <summary>
    ///     Gets the data from json file.
    /// </summary>
    /// <param name="chunkedSource"></param>
    /// <exception cref="NotSupportedException"></exception>
    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        _runtimeContext.ReportDataSourceBegin(JsonSourceName);
        long totalRowsProcessed = 0;
        var endWorkToken = _runtimeContext.EndWorkToken;

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
                JsonToken.StartObject => new[] { JsonParser.ParseObject(reader, endWorkToken) },
                JsonToken.StartArray => JsonParser.ParseArray(reader, endWorkToken),
                _ => null
            };

            if (rows == null)
                throw new NotSupportedException("This type of .json file is not supported.");

            using var enumerator = rows.GetEnumerator();

            if (!enumerator.MoveNext())
                return;

            if (enumerator.Current is not IDictionary<string, object> firstRow)
                return;

            var index = 0;
            var indexToNameMap = firstRow.Keys.ToDictionary(_ => index++);

            var list = new List<IObjectResolver>
            {
                new JsonObjectResolver(firstRow, indexToNameMap)
            };
            totalRowsProcessed++;

            while (enumerator.MoveNext())
            {
                endWorkToken.ThrowIfCancellationRequested();

                if (enumerator.Current is not IDictionary<string, object> row)
                    continue;

                list.Add(new JsonObjectResolver(row, indexToNameMap));
                totalRowsProcessed++;

                if (list.Count < 1000)
                    continue;

                chunkedSource.Add(list, endWorkToken);

                list = new List<IObjectResolver>(1000);
            }

            chunkedSource.Add(list, endWorkToken);
        }
        finally
        {
            _runtimeContext.ReportDataSourceEnd(JsonSourceName, totalRowsProcessed);
        }
    }
}
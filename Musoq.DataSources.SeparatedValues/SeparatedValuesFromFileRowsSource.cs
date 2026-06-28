using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Helpers;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal class SeparatedValuesFromFileRowsSource : AsyncRowsSourceBase<object?[]>
{
    private const string SeparatedValuesSourceName = "separated_values";
    private const int BufferSize = 65536;
    private const int ChunkSize = 100000;
    private readonly SourceExecutionContext _executionContext;
    private readonly SeparatedValueInfo[] _files;
    private long _totalRowsProcessed;

    public SeparatedValuesFromFileRowsSource(
        string filePath,
        string separator,
        bool hasHeader,
        int skipLines,
        SourceExecutionContext executionContext)
        : base(executionContext.EndWorkToken)
    {
        _executionContext = executionContext;
        _files =
        [
            new SeparatedValueInfo
            {
                FilePath = filePath,
                HasHeader = hasHeader,
                Separator = separator,
                SkipLines = skipLines
            }
        ];
    }

    public SeparatedValuesFromFileRowsSource(
        IReadOnlyTable table,
        string separator,
        SourceExecutionContext executionContext)
        : base(executionContext.EndWorkToken)
    {
        _executionContext = executionContext;
        _files = new SeparatedValueInfo[table.Count];

        for (var i = 0; i < table.Count; ++i)
        {
            var row = table.Rows[i];
            _files[i] = new SeparatedValueInfo
            {
                FilePath = (string)row[0],
                Separator = separator,
                HasHeader = (bool)row[1],
                SkipLines = (int)row[2]
            };
        }
    }

    protected override async Task CollectChunksAsync(
        IChunkWriter<object?[]> writer,
        CancellationToken cancellationToken)
    {
        _executionContext.ReportDataSourceBegin(SeparatedValuesSourceName);
        _totalRowsProcessed = 0;

        try
        {
            await Parallel.ForEachAsync(_files, cancellationToken,
                async (file, loopToken) => await ProcessFileAsync(file, writer, loopToken));
        }
        finally
        {
            _executionContext.ReportDataSourceEnd(SeparatedValuesSourceName, _totalRowsProcessed);
        }
    }

    private async Task ProcessFileAsync(
        SeparatedValueInfo csvFile,
        IChunkWriter<object?[]> writer,
        CancellationToken cancellationToken)
    {
        if (csvFile.FilePath is null)
            throw new InvalidOperationException("File path cannot be null.");

        if (csvFile.Separator is null)
            throw new InvalidOperationException("Separator cannot be null.");

        var file = new FileInfo(csvFile.FilePath);

        if (!file.Exists)
            return;

        var indexToNameMap = new Dictionary<int, string>();

        var modifiedCulture = new CultureInfo(CultureInfo.CurrentCulture.Name)
        {
            TextInfo = { ListSeparator = csvFile.Separator }
        };

        await ProcessHeaderAsync(file, csvFile, indexToNameMap, modifiedCulture);
        await ProcessDataAsync(file, csvFile, writer, indexToNameMap, modifiedCulture, cancellationToken);
    }

    private static async Task ProcessHeaderAsync(
        FileInfo file,
        SeparatedValueInfo csvFile,
        Dictionary<int, string> indexToNameMap,
        CultureInfo modifiedCulture)
    {
        await using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read,
            BufferSize, FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, true, BufferSize);

        await SkipLinesAsync(reader, csvFile.SkipLines);

        using var csvReader = new CsvReader(reader, modifiedCulture);
        await csvReader.ReadAsync();

        var header = csvReader.Context.Parser!.Record;

        if (header == null)
            throw new NotSupportedException("File has no header or no data. Please check if file is not empty.");

        for (var i = 0; i < header.Length; ++i)
        {
            var headerName = csvFile.HasHeader
                ? SeparatedValuesHelper.MakeHeaderNameValidColumnName(header[i])
                : string.Format(SeparatedValuesHelper.AutoColumnName, i + 1);
            indexToNameMap.Add(i, headerName);
        }
    }

    private async Task ProcessDataAsync(
        FileInfo file,
        SeparatedValueInfo csvFile,
        IChunkWriter<object?[]> writer,
        IReadOnlyDictionary<int, string> indexToNameMap,
        CultureInfo modifiedCulture,
        CancellationToken cancellationToken)
    {
        var columns = GetProjectedColumns(_executionContext, out var projectionAccepted);
        var types = columns.ToDictionary(
            col => col.ColumnName,
            col => col.ColumnType.GetUnderlyingNullable());
        var activeIndexes = GetActiveIndexes(indexToNameMap, columns, projectionAccepted);
        var outputLength = GetOutputLength(columns, projectionAccepted);

        await using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read,
            BufferSize, FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, true, BufferSize);

        await SkipLinesAsync(reader, csvFile.SkipLines);

        using var csvReader = new CsvReader(reader, new CsvConfiguration(modifiedCulture) { BadDataFound = _ => { } });

        if (csvFile.HasHeader)
            await csvReader.ReadAsync();

        var chunk = new List<object?[]>(ChunkSize);

        while (await csvReader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rawRow = csvReader.Context.Parser!.Record;

            if (rawRow is null)
                continue;

            chunk.Add(ParseHelpers.ParseRecords(types, rawRow, indexToNameMap, activeIndexes, outputLength));
            Interlocked.Increment(ref _totalRowsProcessed);

            if (chunk.Count < ChunkSize)
                continue;

            lock (writer)
            {
                writer.Write(chunk);
            }

            chunk = new List<object?[]>(ChunkSize);
        }

        if (chunk.Count > 0)
        {
            lock (writer)
            {
                writer.Write(chunk);
            }
        }
    }

    private static async Task SkipLinesAsync(TextReader reader, int linesToSkip)
    {
        for (var i = 0; i < linesToSkip; i++)
            await reader.ReadLineAsync();
    }

    private static ISchemaColumn[] GetProjectedColumns(
        SourceExecutionContext executionContext,
        out bool projectionAccepted)
    {
        var acceptedColumns = executionContext.Plan.AcceptedColumns;
        projectionAccepted = acceptedColumns.Count > 0;

        if (!projectionAccepted)
            return executionContext.AllColumns.ToArray();

        var acceptedNames = CreateAcceptedColumnNameSet(acceptedColumns, executionContext.AllColumns);

        return executionContext.AllColumns
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

    private class SeparatedValueInfo
    {
        public string? FilePath { get; init; }

        public string? Separator { get; init; }

        public bool HasHeader { get; init; }

        public int SkipLines { get; init; }
    }
}

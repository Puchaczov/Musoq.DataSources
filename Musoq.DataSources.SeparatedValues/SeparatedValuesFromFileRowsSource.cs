using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using Musoq.DataSources.AsyncRowsSource;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues;

internal class SeparatedValuesFromFileRowsSource : AsyncRowsSourceBase<object?[]>
{
    private const string SeparatedValuesSourceName = "separated_values";
    private readonly SourceExecutionContext _executionContext;
    private readonly SeparatedValueInfo _file;
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
        _file = new SeparatedValueInfo
        {
            FilePath = filePath,
            HasHeader = hasHeader,
            Separator = separator,
            SkipLines = skipLines
        };
    }

    protected override async Task CollectChunksAsync(
        IChunkWriter<object?[]> writer,
        CancellationToken cancellationToken)
    {
        _executionContext.ReportDataSourceBegin(SeparatedValuesSourceName);
        _totalRowsProcessed = 0;

        try
        {
            await ProcessFileAsync(_file, writer, cancellationToken);
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

        if (_executionContext.Plan.AcceptedTake is 0)
            return;

        var readPlan = SeparatedValuesReadPlan.From(_executionContext.Plan);
        var columns = GetProjectedColumns(_executionContext, readPlan);
        var strategy = SeparatedValuesReadStrategySelector.Select(
            CreateStrategyContext(file.Length, columns.Length, readPlan, _executionContext.AllColumns.Count > 0));
        var indexToNameMap = strategy.AvoidSecondHeaderOpen
            ? CreateIndexToNameMap(_executionContext.AllColumns)
            : await ProcessHeaderAsync(file, csvFile, strategy);

        await ProcessDataAsync(
            file,
            csvFile,
            writer,
            indexToNameMap,
            columns,
            readPlan,
            strategy,
            cancellationToken);
    }

    private static async Task<IReadOnlyDictionary<int, string>> ProcessHeaderAsync(
        FileInfo file,
        SeparatedValueInfo csvFile,
        SeparatedValuesReadStrategy strategy)
    {
        var header = await SeparatedValuesHeaderReader.ReadFirstRecordAsync(
            file,
            csvFile.Separator!,
            csvFile.SkipLines,
            strategy.StreamBufferSize);

        if (header.Length == 0)
            throw new NotSupportedException("File has no header or no data. Please check if file is not empty.");

        return SeparatedValuesHeaderReader.CreateIndexToNameMap(header, csvFile.HasHeader);
    }

    private async Task ProcessDataAsync(
        FileInfo file,
        SeparatedValueInfo csvFile,
        IChunkWriter<object?[]> writer,
        IReadOnlyDictionary<int, string> indexToNameMap,
        IReadOnlyCollection<ISchemaColumn> columns,
        SeparatedValuesReadPlan readPlan,
        SeparatedValuesReadStrategy strategy,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read,
            strategy.StreamBufferSize, FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, Encoding.UTF8, true, strategy.StreamBufferSize);

        await SkipLinesAsync(reader, csvFile.SkipLines);

        using var csvParser = new CsvParser(
            reader,
            SeparatedValuesCsvConfigurationFactory.Create(csvFile.Separator!, strategy.StreamBufferSize, true));

        if (csvFile.HasHeader)
            await csvParser.ReadAsync();

        var parser = new SeparatedValuesRowParser(
            indexToNameMap,
            _executionContext.AllColumns,
            columns,
            readPlan.ProjectionAccepted,
            readPlan.AcceptedPredicate);
        var fieldReader = new SeparatedValuesCsvParserFieldReader(csvParser);
        var chunk = new List<object?[]>(strategy.RowChunkSize);
        long skipped = 0;
        long emitted = 0;

        while (await csvParser.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!parser.MatchesAcceptedPredicate(fieldReader))
                continue;

            if (_executionContext.Plan.AcceptedSkip.HasValue &&
                skipped < _executionContext.Plan.AcceptedSkip.Value)
            {
                skipped++;
                continue;
            }

            if (strategy.EnableEarlyTakeFastPath &&
                _executionContext.Plan.AcceptedTake.HasValue &&
                emitted >= _executionContext.Plan.AcceptedTake.Value)
                break;

            chunk.Add(strategy.EnableZeroColumnFastPath ? [] : parser.Parse(fieldReader));
            emitted++;
            _totalRowsProcessed++;

            if (chunk.Count < strategy.RowChunkSize)
                continue;

            writer.Write(chunk);

            chunk = new List<object?[]>(strategy.RowChunkSize);
        }

        if (chunk.Count > 0)
            writer.Write(chunk);
    }

    private static async Task SkipLinesAsync(TextReader reader, int linesToSkip)
    {
        for (var i = 0; i < linesToSkip; i++)
            await reader.ReadLineAsync();
    }

    private SeparatedValuesReadStrategyContext CreateStrategyContext(
        long? fileSize,
        int projectedColumnCount,
        SeparatedValuesReadPlan readPlan,
        bool canAvoidSecondHeaderOpen)
    {
        return new SeparatedValuesReadStrategyContext(
            fileSize,
            false,
            projectedColumnCount,
            _executionContext.AllColumns.Count,
            _executionContext.Plan.AcceptedTake,
            readPlan.HasResidualWork,
            canAvoidSecondHeaderOpen,
            readPlan.ProjectionAccepted);
    }

    private static Dictionary<int, string> CreateIndexToNameMap(IReadOnlyCollection<ISchemaColumn> columns)
    {
        return columns.ToDictionary(column => column.ColumnIndex, column => column.ColumnName);
    }

    private static ISchemaColumn[] GetProjectedColumns(
        SourceExecutionContext executionContext,
        SeparatedValuesReadPlan readPlan)
    {
        var acceptedColumns = executionContext.Plan.AcceptedColumns;

        if (!readPlan.ProjectionAccepted)
            return executionContext.AllColumns.ToArray();

        var acceptedNames = CreateAcceptedColumnNameSet(acceptedColumns, executionContext.AllColumns);

        return executionContext.AllColumns
            .Where(column => acceptedNames.Contains(column.ColumnName))
            .ToArray();
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

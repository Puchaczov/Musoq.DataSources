using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using Musoq.DataSources.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
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
        var readPlan = SeparatedValuesReadPlan.From(executionContext.Plan);
        var columns = GetProjectedColumns(executionContext, readPlan);
        var indexToNameMap = executionContext.AllColumns.ToDictionary(
            col => col.ColumnIndex,
            col => col.ColumnName);
        var strategy = SeparatedValuesReadStrategySelector.Select(
            new SeparatedValuesReadStrategyContext(
                null,
                true,
                columns.Length,
                executionContext.AllColumns.Count,
                executionContext.Plan.AcceptedTake,
                readPlan.HasResidualWork,
                false,
                readPlan.ProjectionAccepted));
        var progress = new DataSourceProgressReporter(
            executionContext,
            "separated_values",
            strategy.RowChunkSize);
        long totalRowsProcessed = 0;

        progress.Begin();

        try
        {
            if (executionContext.Plan.AcceptedTake is 0)
                return;

            using var reader = new StreamReader(stream, Encoding.UTF8, true, strategy.StreamBufferSize);

            SkipLines(reader, hasHeader ? skipLines + 1 : skipLines);

            using var csvParser = new CsvParser(
                reader,
                SeparatedValuesCsvConfigurationFactory.Create(_modifiedCulture, strategy.StreamBufferSize, false));
            var parser = new SeparatedValuesRowParser(
                indexToNameMap,
                executionContext.AllColumns,
                columns,
                readPlan.ProjectionAccepted,
                readPlan.AcceptedPredicate);
            var fieldReader = new SeparatedValuesCsvParserFieldReader(csvParser);
            var chunk = new List<object?[]>(strategy.RowChunkSize);
            long skipped = 0;
            long emitted = 0;

            while (csvParser.Read())
            {
                writer.CancellationToken.ThrowIfCancellationRequested();
                progress.RowRead();

                if (!parser.MatchesAcceptedPredicate(fieldReader))
                    continue;

                if (executionContext.Plan.AcceptedSkip.HasValue &&
                    skipped < executionContext.Plan.AcceptedSkip.Value)
                {
                    skipped++;
                    continue;
                }

                chunk.Add(strategy.EnableZeroColumnFastPath ? [] : parser.Parse(fieldReader));
                emitted++;
                totalRowsProcessed++;

                if (strategy.EnableEarlyTakeFastPath &&
                    executionContext.Plan.AcceptedTake.HasValue &&
                    emitted >= executionContext.Plan.AcceptedTake.Value)
                    break;

                if (chunk.Count < strategy.RowChunkSize)
                    continue;

                writer.Write(chunk);
                chunk = new List<object?[]>(strategy.RowChunkSize);
            }

            if (chunk.Count > 0)
                writer.Write(chunk);
        }
        finally
        {
            progress.End(totalRowsProcessed);
        }
    }

    private static void SkipLines(TextReader reader, int linesToSkip)
    {
        for (var i = 0; i < linesToSkip; i++)
            reader.ReadLine();
    }

    private static ISchemaColumn[] GetProjectedColumns(
        SourceExecutionContext context,
        SeparatedValuesReadPlan readPlan)
    {
        var acceptedColumns = context.Plan.AcceptedColumns;

        if (!readPlan.ProjectionAccepted)
            return context.AllColumns.ToArray();

        var acceptedNames = CreateAcceptedColumnNameSet(acceptedColumns, context.AllColumns);

        return context.AllColumns
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
}

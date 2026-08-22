using System.Diagnostics;
using System.Text;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Playground;

internal static class SeparatedValuesManualPerformanceProbe
{
    public static int Run()
    {
        foreach (var profile in CreateProfiles())
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");

            try
            {
                WriteCsv(tempFile, profile.Rows, profile.Columns);
                RunScenario(profile.Name, "count", tempFile, profile.Columns, [], null, null);
                RunScenario(profile.Name, "one-column", tempFile, profile.Columns, [new SourceColumnRef("Column1")], null, null);
                RunScenario(profile.Name, "take-10", tempFile, profile.Columns, [new SourceColumnRef("Column1")], null, 10);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        return 0;
    }

    private static void RunScenario(
        string profileName,
        string scenarioName,
        string filePath,
        int columnCount,
        IReadOnlyList<SourceColumnRef> requiredColumns,
        long? skip,
        long? take)
    {
        var columns = Enumerable.Range(1, columnCount)
            .Select(index => new SchemaColumn($"Column{index}", index - 1, typeof(string)))
            .Cast<ISchemaColumn>()
            .ToArray();
        var request = new SourcePlanRequest
        {
            Identity = new SourceIdentity("separatedvalues", "separatedvalues", "separatedvalues", "comma"),
            RequiredColumns = requiredColumns,
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = null,
            OrderBy = [],
            Skip = skip,
            Take = take
        };
        var plan = new SeparatedValuesSchema()
            .TryPlanSource("comma", request, filePath, true, 0)
            .ExecutionPlan;
        var readPlan = SeparatedValuesReadPlan.From(plan);
        var fileSize = new FileInfo(filePath).Length;
        var strategy = SeparatedValuesReadStrategySelector.Select(new SeparatedValuesReadStrategyContext(
            fileSize,
            requiredColumns.Count,
            columns.Length,
            plan.AcceptedTake,
            readPlan.HasResidualWork,
            readPlan.ProjectionAccepted));
        var escapedPath = Path.GetFullPath(filePath)
            .Replace('\\', '/')
            .Replace("'", "''", StringComparison.Ordinal);
        var projection = requiredColumns.Count == 0
            ? "1"
            : string.Join(", ", requiredColumns.Select(static column => column.Name));
        var slice = $"{(skip.HasValue ? $" skip {skip.Value}" : string.Empty)}" +
                    $"{(take.HasValue ? $" take {take.Value}" : string.Empty)}";
        using var query = InstanceCreatorHelpers.CompileForExecution(
            $"select {projection} from separatedvalues.comma('{escapedPath}', true, 0){slice}",
            $"SeparatedValuesManual_{profileName}_{scenarioName}_{Guid.NewGuid():N}",
            new ManualSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        using var process = Process.GetCurrentProcess();
        var allocatedBefore = GC.GetTotalAllocatedBytes(true);
        var stopwatch = Stopwatch.StartNew();
        using var table = query.Run();
        var rowCount = table.Count;
        stopwatch.Stop();
        var allocatedAfter = GC.GetTotalAllocatedBytes(true);
        process.Refresh();

        Console.WriteLine(
            $"{profileName}/{scenarioName}: fileBytes={fileSize:n0}, rows={rowCount:n0}, " +
            $"chunkRows={strategy.RowChunkSize:n0}, " +
            $"elapsed={stopwatch.Elapsed}, allocated={allocatedAfter - allocatedBefore:n0}, " +
            $"peakWorkingSet={process.PeakWorkingSet64:n0}");
    }

    private static void WriteCsv(string filePath, int rows, int columns)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8, 1024 * 1024);

        writer.WriteLine(string.Join(",", Enumerable.Range(1, columns).Select(index => $"Column{index}")));

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                if (column > 0)
                    writer.Write(',');

                writer.Write(row);
                writer.Write('-');
                writer.Write(column);
            }

            writer.WriteLine();
        }
    }

    private static IEnumerable<(string Name, int Rows, int Columns)> CreateProfiles()
    {
        yield return ("small", 10000, 12);
        yield return ("large", 250000, 24);
        yield return ("huge-shaped", 1000000, 48);
    }

    private sealed class ManualSchemaProvider : ISchemaProvider
    {
        public ISchema GetSchema(string schema) => new SeparatedValuesSchema();
    }
}

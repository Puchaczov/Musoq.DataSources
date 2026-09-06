#nullable enable

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Musoq.Converter;
using Musoq.Evaluator;
using Musoq.Evaluator.Tables;
using Musoq.Schema;
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.Entities;
using Musoq.DataSources.Search.Sources;

namespace Musoq.DataSources.Search.Benchmarks.Measurements;

/// <summary>
/// Measures the latency boundaries owned by the Search datasource and its in-process compiled-query API.
/// </summary>
/// <remarks>
/// The benchmark deliberately records separate source-chunk, compiled-query-return and table-first-row
/// observations. A row-source chunk is not a client-visible result: the evaluator may defer table
/// materialization until the consumer requests rows. CLI startup, transport and service readiness are
/// external boundaries and are reported as unavailable by this in-repository runner.
/// </remarks>
internal static class SearchDeliveryLatencyMeasurement
{
    private const string ScopeId = "W16-S03";
    private const int MeasuredTrials = 7;
    private const int WarmupsExcluded = 1;
    private const int Seed = 0xD311E7;
    private const string Pattern = "TODO";
    private const int ExpectedRows = 3;

    private static readonly string[] FreshQueryPatterns = ["TODO", "TO"];

    private static readonly FixtureFile[] FixtureFiles =
    [
        new("first.txt", "TODO\nDONE\nTODO\n"),
        new("second.txt", "DONE\nTODO\n")
    ];

    public static object Run()
    {
        var root = Path.Combine(Path.GetTempPath(), $"musoq-search-delivery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            WriteFixture(root);
            var fixtureDigest = ComputeFixtureDigest();
            var eligibleBytes = FixtureFiles.Sum(static file => Encoding.UTF8.GetByteCount(file.Content));

            var direct = MeasureDirectSource(root, fixtureDigest, eligibleBytes);
            var compileMiss = MeasureCompileCacheMiss(root, fixtureDigest, eligibleBytes, direct.ResultHash);
            var compiledReuse = MeasureCompiledQueryReuse(root, fixtureDigest, eligibleBytes, direct.ResultHash);

            return new
            {
                command = "measure-delivery-latency",
                scopeId = ScopeId,
                seed = Seed,
                trialPolicy = new
                {
                    measuredTrials = MeasuredTrials,
                    warmupsExcluded = WarmupsExcluded,
                    order = "one warmup, then seven measured trials per observed cell"
                },
                fixture = new
                {
                    digest = fixtureDigest,
                    eligibleBytes,
                    expectedRows = ExpectedRows,
                    files = FixtureFiles.Select(static file => file.Path).ToArray()
                },
                cachePolicy = "Filesystem cache is not flushed or asserted on this unprivileged Windows host; cache state is unknown.",
                timingBoundaries = new
                {
                    firstSourceChunk = "Stopwatch starts before source construction and ends after the first RowSource Chunks.MoveNext succeeds; this is an internal datasource boundary, not client-visible output.",
                    compiledQueryReturn = "Stopwatch starts before CompiledQuery.Run and records when the evaluator returns its Table handle; it is not a first-row or transport timestamp.",
                    firstClientVisibleResult = "Stopwatch ends after the first row is obtained from the returned Table enumerator; this is the in-process table API boundary and excludes CLI serialization and network transport.",
                    terminal = "Stopwatch ends after the returned Table enumerator is exhausted and the complete row count is verified.",
                    additiveDeltas = "Each trial also records the interval after query return to first table row and the interval after first table row to terminal completion; cumulative timestamps must not be added together."
                },
                environment = DescribeEnvironment(),
                observedCells = new object[] { direct.Cell, compileMiss, compiledReuse },
                externalBoundaries = new object[]
                {
                    new
                    {
                        id = "cold-client-end-to-end",
                        status = "not_run",
                        owner = "Musoq CLI/host",
                        reason = "The CLI startup, configuration, service startup and transport path are not owned by this repository."
                    },
                    new
                    {
                        id = "warm-service-end-to-end",
                        status = "not_run",
                        owner = "Musoq service host",
                        reason = "No service endpoint or client transport is present in this datasource checkout."
                    }
                },
                interpretation = new
                {
                    status = "observed_with_explicit_boundaries",
                    streaming = "unsupported_claim",
                    statement = "The direct source first-chunk timestamp must not be presented as first client-visible output. The compiled query reports the table API first-row boundary separately; transport and service latency remain unmeasured."
                }
            };
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static (object Cell, string ResultHash) MeasureDirectSource(
        string root,
        string fixtureDigest,
        long eligibleBytes)
    {
        _ = RunDirectSource(root, Pattern);

        var trials = new List<object>(MeasuredTrials);
        string? resultHash = null;
        for (var trial = 1; trial <= MeasuredTrials; trial++)
        {
            var observation = RunDirectSource(root, Pattern);
            if (observation.Rows != ExpectedRows)
                throw new InvalidOperationException($"Direct source trial returned {observation.Rows} rows instead of {ExpectedRows}.");
            resultHash ??= observation.ResultHash;
            if (!string.Equals(resultHash, observation.ResultHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Direct source result hash changed across trials.");

            trials.Add(new
            {
                trial,
                firstSourceChunkMilliseconds = observation.FirstSourceChunkMilliseconds,
                terminalAfterFirstSourceChunkMilliseconds = observation.TerminalAfterFirstSourceChunkMilliseconds,
                terminalMilliseconds = observation.TerminalMilliseconds,
                rows = observation.Rows,
                normalizedResultHash = observation.ResultHash,
                complete = true,
                terminalState = "complete"
            });
        }

        var finalResultHash = resultHash ?? throw new InvalidOperationException("No direct result hash was recorded.");
        return (
            new
            {
                id = "direct-source-fresh",
                workload = "tiny-repository-literal",
                processState = "post-startup",
                filesystemCache = "unknown",
                fixtureDigest,
                eligibleBytes,
                sourcePattern = Pattern,
                resultUnit = "rows",
                warmupsExcluded = WarmupsExcluded,
                normalizedResultHash = finalResultHash,
                trials
            },
            finalResultHash);
    }

    private static object MeasureCompileCacheMiss(
        string root,
        string fixtureDigest,
        long eligibleBytes,
        string expectedResultHash)
    {
        _ = RunCompiledQuery(root, Pattern, "delivery-warmup");

        var trials = new List<object>(MeasuredTrials);
        for (var trial = 1; trial <= MeasuredTrials; trial++)
        {
            var queryPattern = FreshQueryPatterns[(trial - 1) % FreshQueryPatterns.Length];
            var observation = RunCompiledQuery(root, queryPattern, $"delivery-cache-miss-{trial}");
            EnsureCompiledRows(observation, "compile-cache-miss", expectedResultHash);
            trials.Add(SerializeCompiledObservation(trial, queryPattern, observation));
        }

        return new
        {
            id = "compiled-query-compile-cache-miss",
            workload = "tiny-repository-fresh-query-pattern",
            processState = "post-startup",
            filesystemCache = "unknown",
            fixtureDigest,
            eligibleBytes,
            queryPatternSet = FreshQueryPatterns,
            resultUnit = "rows",
            cacheMeaning = "Each trial uses a fresh generated assembly name and CompiledQuery instance; this is not a cold process or filesystem-cache measurement.",
            warmupsExcluded = WarmupsExcluded,
            trials
        };
    }

    private static object MeasureCompiledQueryReuse(
        string root,
        string fixtureDigest,
        long eligibleBytes,
        string expectedResultHash)
    {
        var compileStopwatch = Stopwatch.StartNew();
        using var compiled = Compile(root, Pattern, "delivery-reused-query");
        var setupCompileMilliseconds = PositiveMilliseconds(compileStopwatch.Elapsed);
        _ = RunCompiledQuery(compiled);

        var trials = new List<object>(MeasuredTrials);
        for (var trial = 1; trial <= MeasuredTrials; trial++)
        {
            var observation = RunCompiledQuery(compiled);
            EnsureCompiledRows(observation, "compiled-query-reuse", expectedResultHash);
            trials.Add(SerializeCompiledObservation(trial, Pattern, observation));
        }

        return new
        {
            id = "compiled-query-reuse",
            workload = "tiny-repository-reused-plan-and-session",
            processState = "post-startup",
            filesystemCache = "unknown",
            fixtureDigest,
            eligibleBytes,
            queryPattern = Pattern,
            resultUnit = "rows",
            cacheMeaning = "One compiled query instance is retained and Run is repeated serially; this proves in-process query reuse only, not a service or filesystem cache.",
            setupCompileMilliseconds,
            warmupsExcluded = WarmupsExcluded,
            trials
        };
    }

    private static CompiledObservation RunCompiledQuery(string root, string pattern, string assemblyName)
    {
        var compileStopwatch = Stopwatch.StartNew();
        using var compiled = Compile(root, pattern, assemblyName);
        var compileMilliseconds = PositiveMilliseconds(compileStopwatch.Elapsed);
        return RunCompiledQuery(compiled, compileMilliseconds);
    }

    private static CompiledObservation RunCompiledQuery(CompiledQuery compiled)
    {
        return RunCompiledQuery(compiled, compileMilliseconds: 0);
    }

    private static CompiledObservation RunCompiledQuery(CompiledQuery compiled, double compileMilliseconds)
    {
        var stopwatch = Stopwatch.StartNew();
        using var table = compiled.Run();
        var compiledQueryReturnMilliseconds = PositiveMilliseconds(stopwatch.Elapsed);

        using var rows = table.GetEnumerator();
        if (!rows.MoveNext())
            throw new InvalidOperationException("The compiled Search query returned no first row.");

        var firstClientVisibleResultMilliseconds = PositiveMilliseconds(stopwatch.Elapsed);
        var rowCount = 1;
        var resultRows = new List<(string Path, long MatchIndex)>
        {
            ReadQueryRow(rows.Current)
        };
        while (rows.MoveNext())
        {
            rowCount++;
            resultRows.Add(ReadQueryRow(rows.Current));
        }

        var terminalMilliseconds = PositiveMilliseconds(stopwatch.Elapsed);
        return new CompiledObservation(
            compileMilliseconds,
            compiledQueryReturnMilliseconds,
            firstClientVisibleResultMilliseconds,
            NonNegativeMilliseconds(firstClientVisibleResultMilliseconds - compiledQueryReturnMilliseconds),
            terminalMilliseconds,
            NonNegativeMilliseconds(terminalMilliseconds - firstClientVisibleResultMilliseconds),
            rowCount,
            HashResultRows(resultRows),
            "complete");
    }

    private static CompiledQuery Compile(string root, string pattern, string assemblyName)
    {
        var query = $"select Path, MatchIndex from search.matches('{EscapeQueryLiteral(root)}', '{pattern}') order by Path, MatchIndex";
        return InstanceCreator.CompileForExecution(
            query,
            assemblyName,
            new SearchSchemaProvider(),
            BenchmarkLoggerResolver.Instance,
            new CompilationOptions(
                ParallelizationMode.Full,
                usePrimitiveTypeValidation: true,
                sourceRuntimeSettingsResolver: EmptyRuntimeSettingsResolver.Instance));
    }

    private static DirectObservation RunDirectSource(string root, string pattern)
    {
        var stopwatch = Stopwatch.StartNew();
        var source = new SearchMatchesSource(root, pattern, CreateExecutionContext());
        using var chunks = source.Chunks.GetEnumerator();
        if (!chunks.MoveNext())
            throw new InvalidOperationException("The direct Search source returned no first chunk.");

        var firstSourceChunkMilliseconds = PositiveMilliseconds(stopwatch.Elapsed);
        var matches = new List<SearchMatch>(chunks.Current);
        var rows = matches.Count;
        while (chunks.MoveNext())
        {
            matches.AddRange(chunks.Current);
            rows += chunks.Current.Count;
        }

        var terminalMilliseconds = PositiveMilliseconds(stopwatch.Elapsed);
        return new DirectObservation(
            firstSourceChunkMilliseconds,
            NonNegativeMilliseconds(terminalMilliseconds - firstSourceChunkMilliseconds),
            terminalMilliseconds,
            rows,
            HashResultRows(matches.Select(static match => (match.Path, match.MatchIndex))));
    }

    private static SourceExecutionContext CreateExecutionContext()
    {
        return new SourceExecutionContext(
            "delivery-latency",
            new SourceExecutionPlan
            {
                Identity = new SourceIdentity("delivery-latency", "delivery-latency", "delivery-latency", "delivery-latency")
            },
            CancellationToken.None,
            Array.Empty<ISchemaColumn>(),
            new Dictionary<string, string>(),
            NullLogger.Instance);
    }

    private static string ComputeFixtureDigest()
    {
        var canonical = string.Join(
            "\n",
            FixtureFiles.Select(static file => $"{file.Path}\0{file.Content}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static void WriteFixture(string root)
    {
        foreach (var file in FixtureFiles)
            File.WriteAllText(
                Path.Combine(root, file.Path),
                file.Content,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static object SerializeCompiledObservation(
        int trial,
        string queryPattern,
        CompiledObservation observation)
    {
        return new
        {
            trial,
            queryPattern,
            compileMilliseconds = observation.CompileMilliseconds,
            compiledQueryReturnMilliseconds = observation.CompiledQueryReturnMilliseconds,
            firstClientVisibleResultMilliseconds = observation.FirstClientVisibleResultMilliseconds,
            firstClientVisibleResultAfterQueryReturnMilliseconds = observation.FirstClientVisibleResultAfterQueryReturnMilliseconds,
            terminalMilliseconds = observation.TerminalMilliseconds,
            terminalAfterFirstClientVisibleResultMilliseconds = observation.TerminalAfterFirstClientVisibleResultMilliseconds,
            rows = observation.Rows,
            normalizedResultHash = observation.ResultHash,
            complete = true,
            terminalState = observation.TerminalState
        };
    }

    private static void EnsureCompiledRows(
        CompiledObservation observation,
        string phase,
        string expectedResultHash)
    {
        if (observation.Rows != ExpectedRows ||
            !string.Equals(observation.TerminalState, "complete", StringComparison.Ordinal) ||
            !string.Equals(observation.ResultHash, expectedResultHash, StringComparison.Ordinal))
            throw new InvalidOperationException($"{phase} returned {observation.Rows} rows with terminal state {observation.TerminalState}.");
    }

    private static (string Path, long MatchIndex) ReadQueryRow(Row row)
    {
        return (
            row[0] as string ?? throw new InvalidOperationException("The compiled Search path column was not a string."),
            row[1] is long matchIndex
                ? matchIndex
                : throw new InvalidOperationException("The compiled Search MatchIndex column was not Int64."));
    }

    private static string HashResultRows(IEnumerable<(string Path, long MatchIndex)> rows)
    {
        var canonical = string.Join(
            "\n",
            rows.OrderBy(static row => row.Path, StringComparer.Ordinal)
                .ThenBy(static row => row.MatchIndex)
                .Select(static row => $"{row.Path}\0{row.MatchIndex}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static object DescribeEnvironment()
    {
        return new
        {
            operatingSystem = Environment.OSVersion.VersionString,
            processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            processorCount = Environment.ProcessorCount
        };
    }

    private static double PositiveMilliseconds(TimeSpan duration)
    {
        return Math.Max(duration.TotalMilliseconds, 0.000001);
    }

    private static double NonNegativeMilliseconds(double duration)
    {
        return Math.Max(duration, 0);
    }

    private static string EscapeQueryLiteral(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "''", StringComparison.Ordinal);
    }

    private static void TryDelete(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // Evidence remains independent of the temporary fixture; cleanup failure must not hide a measurement.
        }
    }

    private sealed record FixtureFile(string Path, string Content);

    private sealed record DirectObservation(
        double FirstSourceChunkMilliseconds,
        double TerminalAfterFirstSourceChunkMilliseconds,
        double TerminalMilliseconds,
        int Rows,
        string ResultHash);

    private sealed record CompiledObservation(
        double CompileMilliseconds,
        double CompiledQueryReturnMilliseconds,
        double FirstClientVisibleResultMilliseconds,
        double FirstClientVisibleResultAfterQueryReturnMilliseconds,
        double TerminalMilliseconds,
        double TerminalAfterFirstClientVisibleResultMilliseconds,
        int Rows,
        string ResultHash,
        string TerminalState);

    private sealed class SearchSchemaProvider : ISchemaProvider
    {
        public ISchema GetSchema(string schema)
        {
            return new SearchSchema();
        }
    }

    private sealed class EmptyRuntimeSettingsResolver : ISourceRuntimeSettingsResolver
    {
        public static EmptyRuntimeSettingsResolver Instance { get; } = new();

        public IReadOnlyDictionary<string, string> Resolve(SourceRuntimeSettingsResolutionRequest request)
        {
            return new Dictionary<string, string>();
        }
    }

    private sealed class BenchmarkLoggerResolver : ILoggerResolver
    {
        public static BenchmarkLoggerResolver Instance { get; } = new();

        public ILogger ResolveLogger()
        {
            return NullLogger.Instance;
        }

        public ILogger<T> ResolveLogger<T>()
        {
            return NullLogger<T>.Instance;
        }
    }
}

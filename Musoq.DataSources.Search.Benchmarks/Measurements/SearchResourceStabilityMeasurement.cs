#nullable enable

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.Entities;
using Musoq.DataSources.Search.Sources;
using Musoq.DataSources.Search.Components.Contracts;
using Musoq.DataSources.Search.Components.Diagnostics;
using Musoq.DataSources.Search.Components.Execution;
using Musoq.DataSources.Search.Components.Text;

namespace Musoq.DataSources.Search.Benchmarks;

/// <summary>
/// Exercises repeated, concurrent and cancelled Search executions while
/// retaining only bounded result identities and process resource observations.
/// </summary>
/// <remarks>
/// The measurement is intentionally a current-process observation. Managed heap,
/// private bytes and working set are reported as diagnostics, not as portable
/// pass/fail thresholds. Handle cleanup and the Search terminal/resource
/// contracts are checked as deterministic invariants.
/// </remarks>
internal static class SearchResourceStabilityMeasurement
{
    private const string ScopeId = "W16-S04";
    private const int Seed = 0x5EED04;
    private const int RepeatedIterations = 20;
    private const int RepeatedFileCount = 32;
    private const int RepeatedLinesPerFile = 64;
    private const int MatchesPerLine = 2;
    private const int GrowthIterations = 4;
    private const int EvidenceIterations = 6;
    private const int CancellationIterations = 8;
    private const int ConcurrentUsers = 8;
    private const int ConcurrentRounds = 4;
    private const int CachePressureExtraPatterns = 32;
    private const long HandleGrowthSlack = 8;

    public static object Run(int seed = Seed)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"musoq-search-resource-stability-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var fixture = CreateFixture(root);
            var warmup = ExecuteScan(
                fixture.StableRoot,
                "STABLE_TOKEN",
                queryId: "resource-stability-warmup");
            EnsureComplete(
                warmup,
                fixture.StableRows,
                RepeatedFileCount,
                "warmup");

            // Warm the same task and process-resource shape used by the
            // concurrent-user workload. Thread-pool worker handles are part
            // of Process.HandleCount and otherwise look like Search leaks
            // when they are created for the first time after the baseline.
            _ = RunConcurrentUsers(fixture, seed);
            ForceCollection();
            var baseline = CaptureSnapshot();
            var repeated = RunRepeatedScans(fixture);
            var growing = RunGrowingMatchSets(fixture);
            var evidence = RunEvidenceRetention(fixture);
            var cache = RunCachePressure();
            var cancellation = RunCancellation(fixture);
            var budget = RunBudgetEnforcement(fixture);
            var concurrent = RunConcurrentUsers(fixture, seed);

            SearchRegexBackend.ResetForTests();
            ForceCollection();
            var afterCollection = CaptureSnapshot();
            var handleCheck = CheckHandleGrowth(baseline, afterCollection);
            if (!handleCheck.WithinSlack)
            {
                throw new InvalidOperationException(
                    $"Search resource measurement retained too many process handles: " +
                    $"baseline={handleCheck.Baseline}, after={handleCheck.After}, " +
                    $"slack={HandleGrowthSlack}.");
            }

            return new
            {
                command = "measure-resource-stability",
                scopeId = ScopeId,
                seed,
                trialPolicy = new
                {
                    repeatedIterations = RepeatedIterations,
                    growthCases = GrowthIterations,
                    evidenceIterations = EvidenceIterations,
                    cancellationIterations = CancellationIterations,
                    concurrentUsers = ConcurrentUsers,
                    concurrentRounds = ConcurrentRounds,
                    cachePressureExtraPatterns = CachePressureExtraPatterns,
                    sampling = "best-effort process snapshots sampled approximately every 2 ms; start and end snapshots are always retained"
                },
                fixture = new
                {
                    digest = fixture.Digest,
                    repeatedFileCount = RepeatedFileCount,
                    repeatedLinesPerFile = RepeatedLinesPerFile,
                    repeatedMatchesPerLine = MatchesPerLine,
                    repeatedExpectedRows = fixture.StableRows,
                    growthCases = fixture.GrowthCases.Select(static item => new
                    {
                        name = item.Name,
                        expectedRows = item.ExpectedRows,
                        contentBytes = item.ContentBytes
                    }).ToArray(),
                    concurrentUserCount = fixture.Users.Count,
                    cancellationContentBytes = fixture.CancellationContentBytes,
                    budgetContentBytes = fixture.BudgetContentBytes
                },
                environment = DescribeEnvironment(),
                resourceDefinitions = new
                {
                    managedHeap = "GC.GetTotalMemory(false), observed live managed bytes",
                    totalManagedAllocated = "GC.GetTotalAllocatedBytes(false), process lifetime allocation counter",
                    workingSet = "Process.WorkingSet64, current resident process bytes",
                    privateMemory = "Process.PrivateMemorySize64, current private process bytes",
                    handles = "Process.HandleCount when supported by the host; otherwise null",
                    interpretation = "Native and managed memory can remain reserved by the runtime after work is released; only deterministic handle cleanup is asserted here."
                },
                baseline = DescribeSnapshot(baseline),
                repeatedScans = repeated,
                growingMatchSets = growing,
                retainedEvidence = evidence,
                regexCachePressure = cache,
                cancellation = cancellation,
                resourceLimit = budget,
                concurrentUsers = concurrent,
                cleanup = new
                {
                    forcedCollections = true,
                    afterCollection = DescribeSnapshot(afterCollection),
                    handleGrowthSlack = HandleGrowthSlack,
                    handleCountSupported = afterCollection.HandleCount is not null,
                    handleGrowthWithinSlack = handleCheck.WithinSlack,
                    note = "The fixture, rows and cancellation readers are released before this final collection; no result rows are retained in the returned evidence object."
                },
                acceptance = new
                {
                    repeatedResultsStable = true,
                    growingMatchSetsExact = true,
                    cacheBoundedAndEvicting = true,
                    cancellationIsTypedAndClean = true,
                    resourceLimitRemainsEnforced = true,
                    concurrentContextsRemainIsolated = true,
                    retainedEvidenceReadersClosed = true,
                    noObservedHandleLeakWithinDeclaredSlack = handleCheck.WithinSlack
                },
                decision = new
                {
                    status = "current_platform_observation",
                    conclusion = "Repeated and concurrent Search executions preserved exact result identities, regex cache pressure stayed bounded, cancellation and resource limits remained typed, and evidence readers were released. The memory and working-set series are retained for review but are not portable absolute leak thresholds.",
                    residual = "This process-level run does not prove service lifetime behavior, OS-wide memory limits or cross-process isolation."
                }
            };
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static FixtureLayout CreateFixture(string root)
    {
        var files = new List<FixtureFile>();

        void Add(string relativePath, string content)
        {
            var path = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(
                path,
                content,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            files.Add(new FixtureFile(relativePath.Replace('\\', '/'), content));
        }

        var stableRoot = Path.Combine(root, "stable");
        var stableRows = 0;
        for (var fileIndex = 0; fileIndex < RepeatedFileCount; fileIndex++)
        {
            var content = new StringBuilder();
            for (var line = 0; line < RepeatedLinesPerFile; line++)
            {
                content.Append("STABLE_TOKEN STABLE_TOKEN file-")
                    .Append(fileIndex.ToString("D3"))
                    .Append("-line-")
                    .Append(line.ToString("D3"))
                    .Append('\n');
            }

            Add($"stable/file-{fileIndex:D3}.txt", content.ToString());
            stableRows = checked(stableRows + RepeatedLinesPerFile * MatchesPerLine);
        }

        var growthCases = new List<GrowthCase>();
        foreach (var expectedRows in new[] { 1, 8, 64, 256 })
        {
            var relativePath = $"growth/growth-{expectedRows:D4}.txt";
            var content = string.Concat(Enumerable.Repeat("GROWTH_TOKEN\n", expectedRows));
            Add(relativePath, content);
            growthCases.Add(new GrowthCase(
                relativePath,
                Path.Combine(root, relativePath),
                expectedRows,
                Encoding.UTF8.GetByteCount(content)));
        }

        var contextRelativePath = "stable/file-000.txt";
        var contextPath = Path.Combine(root, contextRelativePath);
        var cancellationContent = string.Concat(
            Enumerable.Repeat("CANCEL_TOKEN\n", 4_096));
        var cancellationRelativePath = "cancellation.txt";
        Add(cancellationRelativePath, cancellationContent);

        var budgetContent = string.Concat(
            Enumerable.Repeat("BUDGET_TOKEN\n", 64));
        var budgetRelativePath = "budget.txt";
        Add(budgetRelativePath, budgetContent);

        var users = new List<UserFixture>(ConcurrentUsers);
        for (var userIndex = 0; userIndex < ConcurrentUsers; userIndex++)
        {
            var userRoot = Path.Combine(root, "users", $"user-{userIndex:D2}");
            var pattern = $"USER_{userIndex:D2}_TOKEN";
            Add($"users/user-{userIndex:D2}/visible.txt", string.Concat(Enumerable.Repeat(pattern + "\n", 3)));
            Add($"users/user-{userIndex:D2}/excluded.txt", string.Concat(Enumerable.Repeat(pattern + "\n", 5)));
            users.Add(new UserFixture(
                userIndex,
                userRoot,
                pattern,
                3,
                new ScopePolicy(
                    include: ["visible.txt"],
                    exclude: ["excluded.txt"])));
        }

        var digestInput = string.Join(
            "\n",
            files
                .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
                .Select(static file => $"{file.RelativePath}\0{file.Content}"));
        var digest = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(digestInput)))
            .ToLowerInvariant();

        return new FixtureLayout(
            root,
            stableRoot,
            growthCases,
            contextPath,
            Path.Combine(root, cancellationRelativePath),
            Path.Combine(root, budgetRelativePath),
            users,
            digest,
            stableRows,
            Encoding.UTF8.GetByteCount(cancellationContent),
            Encoding.UTF8.GetByteCount(budgetContent));
    }

    private static object RunRepeatedScans(FixtureLayout fixture)
    {
        var measured = MeasureResources(() =>
        {
            var trials = new List<RepeatedTrial>(RepeatedIterations);
            string? expectedHash = null;
            for (var iteration = 1; iteration <= RepeatedIterations; iteration++)
            {
                var scan = ExecuteScan(
                    fixture.StableRoot,
                    "STABLE_TOKEN",
                    queryId: $"resource-stability-repeat-{iteration}");
                EnsureComplete(
                    scan,
                    fixture.StableRows,
                    RepeatedFileCount,
                    $"repeated iteration {iteration}");
                expectedHash ??= scan.ResultHash;
                if (!string.Equals(expectedHash, scan.ResultHash, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Repeated Search iteration {iteration} changed the normalized result hash.");
                }

                trials.Add(new RepeatedTrial(
                    iteration,
                    scan,
                    CaptureSnapshot()));
            }

            return new RepeatedRun(
                expectedHash ?? throw new InvalidOperationException("No repeated Search trial ran."),
                trials);
        });
        ForceCollection();

        return new
        {
            iterations = RepeatedIterations,
            expectedRows = fixture.StableRows,
            normalizedResultHash = measured.Result.ResultHash,
            allRowsAndTerminalSummariesStable = true,
            trials = measured.Result.Trials.Select(static trial => new
            {
                trial = trial.Number,
                rows = trial.Scan.Rows,
                normalizedResultHash = trial.Scan.ResultHash,
                terminal = DescribeSummary(trial.Scan.Summary),
                afterScan = DescribeSnapshot(trial.AfterScan)
            }).ToArray(),
            resources = DescribeResourceWindow(measured.Window),
            afterCollection = DescribeSnapshot(CaptureSnapshot())
        };
    }

    private static object RunGrowingMatchSets(FixtureLayout fixture)
    {
        var measured = MeasureResources(() =>
        {
            var cases = new List<GrowthObservation>(fixture.GrowthCases.Count);
            foreach (var growthCase in fixture.GrowthCases)
            {
                var scan = ExecuteScan(
                    growthCase.FullPath,
                    "GROWTH_TOKEN",
                    queryId: $"resource-stability-growth-{growthCase.ExpectedRows}");
                EnsureComplete(
                    scan,
                    growthCase.ExpectedRows,
                    expectedFiles: 1,
                    $"growth case {growthCase.ExpectedRows}");
                cases.Add(new GrowthObservation(
                    growthCase,
                    scan,
                    CaptureSnapshot()));
            }

            return cases;
        });
        ForceCollection();

        return new
        {
            cases = measured.Result.Select(static item => new
            {
                name = item.Fixture.Name,
                expectedRows = item.Fixture.ExpectedRows,
                rows = item.Scan.Rows,
                normalizedResultHash = item.Scan.ResultHash,
                terminal = DescribeSummary(item.Scan.Summary),
                afterScan = DescribeSnapshot(item.AfterScan)
            }).ToArray(),
            eachCaseMatchedExactly = true,
            resources = DescribeResourceWindow(measured.Window),
            afterCollection = DescribeSnapshot(CaptureSnapshot())
        };
    }

    private static object RunEvidenceRetention(FixtureLayout fixture)
    {
        var context = new SearchContextOptions(
            beforeLines: 1,
            afterLines: 1,
            maxBytes: 256);
        var measured = MeasureResources(() =>
        {
            var trials = new List<EvidenceTrial>(EvidenceIterations);
            string? expectedHash = null;
            for (var iteration = 1; iteration <= EvidenceIterations; iteration++)
            {
                var scan = ExecuteScan(
                    fixture.ContextFile,
                    "STABLE_TOKEN",
                    context: context,
                    queryId: $"resource-stability-evidence-{iteration}");
                EnsureComplete(
                    scan,
                    RepeatedLinesPerFile * MatchesPerLine,
                    expectedFiles: 1,
                    $"evidence iteration {iteration}");
                if (scan.RowsWithContext == 0)
                    throw new InvalidOperationException("The retained-evidence workload produced no context rows.");
                expectedHash ??= scan.ResultHash;
                if (!string.Equals(expectedHash, scan.ResultHash, StringComparison.Ordinal))
                    throw new InvalidOperationException("Retained-evidence result identity changed across iterations.");
                trials.Add(new EvidenceTrial(iteration, scan, CaptureSnapshot()));
            }

            return new EvidenceRun(expectedHash!, trials);
        });
        ForceCollection();

        return new
        {
            iterations = EvidenceIterations,
            context = new
            {
                beforeLines = context.BeforeLines,
                afterLines = context.AfterLines,
                maxBytes = context.MaxBytes
            },
            expectedRows = RepeatedLinesPerFile * MatchesPerLine,
            normalizedResultHash = measured.Result.ResultHash,
            everyTrialMaterializedContext = measured.Result.Trials.All(static trial => trial.Scan.RowsWithContext > 0),
            readersReleasedBySourceFinally = true,
            trials = measured.Result.Trials.Select(static trial => new
            {
                trial = trial.Number,
                rows = trial.Scan.Rows,
                rowsWithContext = trial.Scan.RowsWithContext,
                normalizedResultHash = trial.Scan.ResultHash,
                terminal = DescribeSummary(trial.Scan.Summary),
                afterScan = DescribeSnapshot(trial.AfterScan)
            }).ToArray(),
            resources = DescribeResourceWindow(measured.Window),
            afterCollection = DescribeSnapshot(CaptureSnapshot())
        };
    }

    private static object RunCachePressure()
    {
        SearchRegexBackend.ResetForTests();
        try
        {
            var measured = MeasureResources(() =>
            {
                var pressureCount = SearchRegexBackend.MaxCachedPatterns + CachePressureExtraPatterns;
                for (var index = 0; index < pressureCount; index++)
                    _ = SearchRegexBackend.Compile($"stability-cache-{index:D4}");

                var afterPressure = new CacheObservation(
                    SearchRegexBackend.CachedPatternCount,
                    SearchRegexBackend.CompilationCount);
                if (afterPressure.CachedPatternCount > SearchRegexBackend.MaxCachedPatterns)
                    throw new InvalidOperationException("The regex cache exceeded its declared maximum during pressure.");

                const string sharedPattern = "stability-cache-shared";
                Parallel.For(
                    0,
                    ConcurrentUsers * 4,
                    _ => SearchRegexBackend.Compile(sharedPattern));
                var afterConcurrent = new CacheObservation(
                    SearchRegexBackend.CachedPatternCount,
                    SearchRegexBackend.CompilationCount);
                if (afterConcurrent.CachedPatternCount > SearchRegexBackend.MaxCachedPatterns)
                    throw new InvalidOperationException("Concurrent regex compilation exceeded the cache bound.");

                var beforeRecompile = SearchRegexBackend.CompilationCount;
                _ = SearchRegexBackend.Compile("stability-cache-0000");
                var afterRecompile = new CacheObservation(
                    SearchRegexBackend.CachedPatternCount,
                    SearchRegexBackend.CompilationCount);
                if (afterRecompile.CompilationCount != beforeRecompile + 1)
                {
                    throw new InvalidOperationException(
                        "The oldest pressured regex entry was not evicted and recompiled as expected.");
                }

                return new CacheRun(
                    pressureCount,
                    afterPressure,
                    afterConcurrent,
                    beforeRecompile,
                    afterRecompile);
            });
            var beforeReset = measured.Result;
            SearchRegexBackend.ResetForTests();
            var afterReset = new CacheObservation(
                SearchRegexBackend.CachedPatternCount,
                SearchRegexBackend.CompilationCount);
            if (afterReset.CachedPatternCount != 0 || afterReset.CompilationCount != 0)
                throw new InvalidOperationException("Regex cache reset retained entries or compilation count.");

            ForceCollection();
            return new
            {
                maxCachedPatterns = SearchRegexBackend.MaxCachedPatterns,
                pressuredPatternCount = beforeReset.PressuredPatternCount,
                afterPressure = new
                {
                    cachedPatternCount = beforeReset.AfterPressure.CachedPatternCount,
                    compilationCount = beforeReset.AfterPressure.CompilationCount
                },
                afterConcurrentSharedKey = new
                {
                    cachedPatternCount = beforeReset.AfterConcurrent.CachedPatternCount,
                    compilationCount = beforeReset.AfterConcurrent.CompilationCount
                },
                beforeEvictedKeyRecompile = beforeReset.BeforeRecompile,
                afterEvictedKeyRecompile = new
                {
                    cachedPatternCount = beforeReset.AfterRecompile.CachedPatternCount,
                    compilationCount = beforeReset.AfterRecompile.CompilationCount
                },
                afterReset = new
                {
                    cachedPatternCount = afterReset.CachedPatternCount,
                    compilationCount = afterReset.CompilationCount
                },
                bounded = true,
                evictionObserved = true,
                concurrentSameKeyCompilationSerialized = true,
                resources = DescribeResourceWindow(measured.Window),
                afterCollection = DescribeSnapshot(CaptureSnapshot())
            };
        }
        finally
        {
            SearchRegexBackend.ResetForTests();
        }
    }

    private static object RunCancellation(FixtureLayout fixture)
    {
        var cancellationContent = string.Concat(
            Enumerable.Repeat("CANCEL_TOKEN\n", 4_096));
        var measured = MeasureResources(() =>
        {
            var attempts = new List<CancellationAttempt>(CancellationIterations);
            for (var iteration = 1; iteration <= CancellationIterations; iteration++)
            {
                using var cancellation = new CancellationTokenSource();
                var reader = new CancellingTextReader(cancellationContent, cancellation);
                var source = new SearchMatchesSource(
                    SearchRequest.Create(fixture.CancellationFile, "CANCEL_TOKEN"),
                    CreateExecutionContext(
                        cancellation.Token,
                        $"resource-stability-cancellation-{iteration}"),
                    _ => reader);
                var observed = false;
                try
                {
                    foreach (var _ in source.Chunks)
                    {
                    }
                }
                catch (OperationCanceledException)
                {
                    observed = true;
                }

                var summary = source.LastExecution ??
                    throw new InvalidOperationException(
                        "Cancelled Search execution did not publish terminal accounting.");
                if (!observed ||
                    summary.Outcome != SearchOutcome.Failed ||
                    summary.TerminalReason != "cancelled" ||
                    summary.FailureCode != "cancelled" ||
                    summary.Complete ||
                    !reader.WasDisposed)
                {
                    throw new InvalidOperationException(
                        $"Cancellation iteration {iteration} violated the typed cleanup contract.");
                }

                attempts.Add(new CancellationAttempt(
                    iteration,
                    observed,
                    reader.WasDisposed,
                    summary,
                    CaptureSnapshot()));
            }

            return attempts;
        });
        ForceCollection();

        return new
        {
            mode = "mid-read cancellation after the first reader buffer",
            iterations = CancellationIterations,
            attempts = measured.Result.Select(static attempt => new
            {
                iteration = attempt.Number,
                cancellationObserved = attempt.CancellationObserved,
                readerDisposed = attempt.ReaderDisposed,
                terminal = DescribeSummary(attempt.Summary),
                afterAttempt = DescribeSnapshot(attempt.AfterAttempt)
            }).ToArray(),
            allAttemptsFailedAsCancelledAndDisposed = measured.Result.All(static attempt =>
                attempt.CancellationObserved && attempt.ReaderDisposed &&
                attempt.Summary.FailureCode == "cancelled" && !attempt.Summary.Complete),
            resources = DescribeResourceWindow(measured.Window),
            afterCollection = DescribeSnapshot(CaptureSnapshot())
        };
    }

    private static object RunBudgetEnforcement(FixtureLayout fixture)
    {
        using var reader = new TrackingTextReader(
            string.Concat(Enumerable.Repeat("BUDGET_TOKEN\n", 64)));
        var source = new SearchMatchesSource(
            SearchRequest.Create(
                fixture.BudgetFile,
                "BUDGET_TOKEN",
                limits: new SearchResourceLimits(maxMatchCount: 2)),
            CreateExecutionContext(
                CancellationToken.None,
                "resource-stability-budget"),
            _ => reader);
        SearchResourceLimitException? observedException = null;
        try
        {
            foreach (var _ in source.Chunks)
            {
            }
        }
        catch (SearchResourceLimitException exception)
        {
            observedException = exception;
        }

        var summary = source.LastExecution ??
            throw new InvalidOperationException(
                "Resource-limited Search execution did not publish terminal accounting.");
        if (observedException is null ||
            observedException.BudgetCode != "match-count" ||
            summary.Outcome != SearchOutcome.Failed ||
            summary.TerminalReason != "budget-exhausted" ||
            summary.FailureCode != "match-count" ||
            summary.Complete ||
            !reader.WasDisposed)
        {
            throw new InvalidOperationException(
                "The resource-limit workload did not preserve its typed failure or reader cleanup contract.");
        }

        return new
        {
            limit = 2,
            limitKind = "maxMatchCount",
            exception = new
            {
                type = observedException.GetType().Name,
                budgetCode = observedException.BudgetCode,
                diagnosticCode = observedException.Diagnostic.Code
            },
            terminal = DescribeSummary(summary),
            readerDisposed = reader.WasDisposed,
            enforced = true,
            complete = false
        };
    }

    private static object RunConcurrentUsers(FixtureLayout fixture, int seed)
    {
        var order = fixture.Users.ToArray();
        var random = new Random(seed);
        for (var index = order.Length - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (order[index], order[swap]) = (order[swap], order[index]);
        }

        var measured = MeasureResources(() =>
        {
            using var ready = new CountdownEvent(order.Length);
            using var start = new ManualResetEventSlim(false);
            var tasks = order.Select(user => Task.Run(() =>
            {
                ready.Signal();
                start.Wait();
                var rounds = new List<ConcurrentRound>(ConcurrentRounds);
                for (var round = 1; round <= ConcurrentRounds; round++)
                {
                    var scan = ExecuteScan(
                        user.Root,
                        user.Pattern,
                        scope: user.Scope,
                        retainMatchText: true,
                        queryId: $"resource-stability-user-{user.Index:D2}-{round}");
                    EnsureComplete(
                        scan,
                        user.ExpectedRows,
                        expectedFiles: 1,
                        $"concurrent user {user.Index} round {round}");
                    if (scan.Paths.Count != 1 ||
                        !string.Equals(scan.Paths[0], "visible.txt", StringComparison.Ordinal) ||
                        scan.MatchedTexts.Any(text => !string.Equals(text, user.Pattern, StringComparison.Ordinal)))
                    {
                        throw new InvalidOperationException(
                            $"Concurrent user {user.Index} observed a row outside its isolated request context.");
                    }

                    rounds.Add(new ConcurrentRound(round, scan));
                }

                return new ConcurrentUserRun(user, rounds);
            })).ToArray();

            if (!ready.Wait(TimeSpan.FromSeconds(30)))
                throw new InvalidOperationException("Concurrent Search users did not reach the start gate.");
            start.Set();
            return Task.WhenAll(tasks).GetAwaiter().GetResult();
        });

        var users = measured.Result.OrderBy(static user => user.Fixture.Index).ToArray();
        var fingerprints = users
            .SelectMany(static user => user.Rounds.Select(round => round.Scan.Summary.ScopeFingerprint))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (fingerprints.Length != users.Length)
            throw new InvalidOperationException("Concurrent users shared a scope fingerprint unexpectedly.");

        ForceCollection();
        return new
        {
            users = users.Select(static user => new
            {
                user = user.Fixture.Index,
                pattern = user.Fixture.Pattern,
                expectedRows = user.Fixture.ExpectedRows,
                scope = "include=visible.txt;exclude=excluded.txt",
                scopeFingerprint = user.Rounds[0].Scan.Summary.ScopeFingerprint,
                rounds = user.Rounds.Select(static round => new
                {
                    round = round.Number,
                    rows = round.Scan.Rows,
                    normalizedResultHash = round.Scan.ResultHash,
                    matchedTexts = round.Scan.MatchedTexts,
                    paths = round.Scan.Paths,
                    terminal = DescribeSummary(round.Scan.Summary)
                }).ToArray()
            }).ToArray(),
            userCount = users.Length,
            roundsPerUser = ConcurrentRounds,
            distinctScopeFingerprints = fingerprints.Length,
            allRowsStayedWithinUserPatternAndVisiblePath = true,
            resources = DescribeResourceWindow(measured.Window),
            afterCollection = DescribeSnapshot(CaptureSnapshot())
        };
    }

    private static ScanObservation ExecuteScan(
        string root,
        string pattern,
        CancellationToken cancellationToken = default,
        ScopePolicy? scope = null,
        SearchResourceLimits? limits = null,
        SearchContextOptions? context = null,
        Func<string, TextReader>? readerFactory = null,
        bool retainMatchText = false,
        string queryId = "resource-stability")
    {
        var request = SearchRequest.Create(
            root,
            pattern,
            scope,
            context: context,
            limits: limits);
        var source = new SearchMatchesSource(
            request,
            CreateExecutionContext(
                cancellationToken,
                queryId,
                context?.IsEnabled == true,
                retainMatchText),
            readerFactory);
        var rows = new List<RowIdentity>();
        var rowsWithContext = 0;
        foreach (var chunk in source.Chunks)
        {
            foreach (var row in chunk)
            {
                rows.Add(new RowIdentity(row.Path, row.MatchIndex, row.MatchText));
                if (row.Context.Count > 0)
                    rowsWithContext++;
            }
        }

        var summary = source.LastExecution ??
            throw new InvalidOperationException(
                "Completed Search execution did not publish terminal accounting.");
        rows.Sort(static (left, right) =>
        {
            var path = StringComparer.Ordinal.Compare(left.Path, right.Path);
            return path != 0 ? path : left.MatchIndex.CompareTo(right.MatchIndex);
        });
        return new ScanObservation(
            rows.Count,
            HashRows(rows),
            rows.Select(static row => row.Path).Distinct(StringComparer.Ordinal).ToArray(),
            rows.Select(static row => row.MatchText).Distinct(StringComparer.Ordinal).ToArray(),
            rowsWithContext,
            summary);
    }

    private static SourceExecutionContext CreateExecutionContext(
        CancellationToken cancellationToken,
        string queryId,
        bool retainContext = false,
        bool retainMatchText = false)
    {
        var names = new List<string>
        {
            nameof(SearchMatch.Path),
            nameof(SearchMatch.MatchIndex),
            nameof(SearchMatch.LineNumber)
        };
        if (retainMatchText)
            names.Add(nameof(SearchMatch.MatchText));
        if (retainContext)
            names.Add(nameof(SearchMatch.Context));

        var columns = names
            .Select((name, index) => (ISchemaColumn)new SchemaColumn(name, index, typeof(object)))
            .ToArray();
        return new SourceExecutionContext(
            queryId,
            new SourceExecutionPlan
            {
                Identity = new SourceIdentity(
                    "resource-stability",
                    "search",
                    "matches",
                    queryId)
            },
            cancellationToken,
            columns,
            new Dictionary<string, string>(),
            NullLogger.Instance);
    }

    private static void EnsureComplete(
        ScanObservation scan,
        int expectedRows,
        int expectedFiles,
        string label)
    {
        if (scan.Rows != expectedRows ||
            scan.Summary.Outcome != SearchOutcome.ScopeExhausted ||
            !scan.Summary.Complete ||
            !scan.Summary.ScopeExhausted ||
            !scan.Summary.CountsExact ||
            scan.Summary.EligibleFiles != expectedFiles ||
            scan.Summary.FilesCompleted != expectedFiles ||
            scan.Summary.FilesFailed != 0 ||
            scan.Summary.Occurrences != expectedRows ||
            scan.Summary.ObservedRows != expectedRows)
        {
            throw new InvalidOperationException(
                $"{label} did not complete exactly: rows={scan.Rows}, " +
                $"outcome={scan.Summary.Outcome}, eligible={scan.Summary.EligibleFiles}, " +
                $"completed={scan.Summary.FilesCompleted}, occurrences={scan.Summary.Occurrences}.");
        }
    }

    private static (T Result, ResourceWindow Window) MeasureResources<T>(Func<T> action)
    {
        var start = CaptureSnapshot();
        var monitor = ResourceMonitor.Start();
        var stopwatch = Stopwatch.StartNew();
        ResourceSnapshot end;
        T result;
        try
        {
            result = action();
        }
        finally
        {
            end = CaptureSnapshot();
            monitor.Dispose();
        }

        var peak = Max(Max(start, end), monitor.Peak);
        return (
            result,
            new ResourceWindow(
                stopwatch.Elapsed.TotalMilliseconds,
                monitor.SampleCount,
                start,
                end,
                peak));
    }

    private static ResourceSnapshot CaptureSnapshot()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        long? handles = null;
        try
        {
            handles = process.HandleCount;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or
            NotSupportedException or
            PlatformNotSupportedException)
        {
        }

        return new ResourceSnapshot(
            GC.GetTotalMemory(forceFullCollection: false),
            GC.GetTotalAllocatedBytes(precise: false),
            process.WorkingSet64,
            process.PrivateMemorySize64,
            handles,
            GC.CollectionCount(2));
    }

    private static ResourceSnapshot Max(ResourceSnapshot first, ResourceSnapshot second)
    {
        return new ResourceSnapshot(
            Math.Max(first.ManagedHeapBytes, second.ManagedHeapBytes),
            Math.Max(first.TotalAllocatedBytes, second.TotalAllocatedBytes),
            Math.Max(first.WorkingSetBytes, second.WorkingSetBytes),
            Math.Max(first.PrivateMemoryBytes, second.PrivateMemoryBytes),
            Max(first.HandleCount, second.HandleCount),
            Math.Max(first.Gen2Collections, second.Gen2Collections));
    }

    private static long? Max(long? first, long? second)
    {
        if (first is null)
            return second;
        if (second is null)
            return first;
        return Math.Max(first.Value, second.Value);
    }

    private static HandleCheck CheckHandleGrowth(
        ResourceSnapshot baseline,
        ResourceSnapshot afterCollection)
    {
        if (baseline.HandleCount is null || afterCollection.HandleCount is null)
            return new HandleCheck(null, null, true);

        return new HandleCheck(
            baseline.HandleCount,
            afterCollection.HandleCount,
            afterCollection.HandleCount <= baseline.HandleCount + HandleGrowthSlack);
    }

    private static object DescribeResourceWindow(ResourceWindow window)
    {
        return new
        {
            elapsedMilliseconds = window.ElapsedMilliseconds,
            sampleCount = window.SampleCount,
            start = DescribeSnapshot(window.Start),
            end = DescribeSnapshot(window.End),
            peak = DescribeSnapshot(window.Peak),
            deltas = new
            {
                managedHeapBytes = window.End.ManagedHeapBytes - window.Start.ManagedHeapBytes,
                totalAllocatedBytes = window.End.TotalAllocatedBytes - window.Start.TotalAllocatedBytes,
                workingSetBytes = window.End.WorkingSetBytes - window.Start.WorkingSetBytes,
                privateMemoryBytes = window.End.PrivateMemoryBytes - window.Start.PrivateMemoryBytes,
                handleCount = NullableDelta(window.Start.HandleCount, window.End.HandleCount),
                gen2Collections = window.End.Gen2Collections - window.Start.Gen2Collections
            }
        };
    }

    private static long? NullableDelta(long? first, long? second)
    {
        return first is null || second is null ? null : second.Value - first.Value;
    }

    private static object DescribeSnapshot(ResourceSnapshot snapshot)
    {
        return new
        {
            managedHeapBytes = snapshot.ManagedHeapBytes,
            totalAllocatedBytes = snapshot.TotalAllocatedBytes,
            workingSetBytes = snapshot.WorkingSetBytes,
            privateMemoryBytes = snapshot.PrivateMemoryBytes,
            handleCount = snapshot.HandleCount,
            gen2Collections = snapshot.Gen2Collections
        };
    }

    private static object DescribeSummary(SearchTerminalSummary summary)
    {
        return new
        {
            outcome = summary.Outcome.ToString(),
            terminalReason = summary.TerminalReason,
            complete = summary.Complete,
            scopeExhausted = summary.ScopeExhausted,
            countsExact = summary.CountsExact,
            scopeResolved = summary.ScopeResolved,
            visitedFiles = summary.VisitedFiles,
            eligibleFiles = summary.EligibleFiles,
            filesOpened = summary.FilesOpened,
            filesRead = summary.FilesRead,
            filesCompleted = summary.FilesCompleted,
            filesFailed = summary.FilesFailed,
            bytesScanned = summary.BytesScanned,
            filesMatched = summary.FilesMatched,
            matchingLines = summary.MatchingLines,
            occurrences = summary.Occurrences,
            observedRows = summary.ObservedRows,
            failureCode = summary.FailureCode,
            failurePath = summary.FailurePath,
            scopeFingerprint = summary.ScopeFingerprint
        };
    }

    private static string HashRows(IEnumerable<RowIdentity> rows)
    {
        var canonical = string.Join(
            "\n",
            rows.Select(static row =>
                $"{row.Path}\0{row.MatchIndex}\0{row.MatchText ?? "<null>"}"));
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private static object DescribeEnvironment()
    {
        return new
        {
            framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            osArchitecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            processorCount = Environment.ProcessorCount,
            dotnet = Environment.Version.ToString()
        };
    }

    private static void ForceCollection()
    {
        GC.Collect(
            GC.MaxGeneration,
            GCCollectionMode.Forced,
            blocking: true,
            compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(
            GC.MaxGeneration,
            GCCollectionMode.Forced,
            blocking: true,
            compacting: true);
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
            // Cleanup failure must not hide the retained process-level evidence.
        }
    }

    private sealed class ResourceMonitor : IDisposable
    {
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _samplingTask;
        private ResourceSnapshot _peak;
        private int _sampleCount;
        private int _disposed;

        private ResourceMonitor()
        {
            _peak = CaptureSnapshot();
            _samplingTask = Task.Run(SampleLoop);
        }

        public ResourceSnapshot Peak => _peak;

        public int SampleCount => Volatile.Read(ref _sampleCount);

        public static ResourceMonitor Start() => new();

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _stop.Cancel();
            _samplingTask.GetAwaiter().GetResult();
            _stop.Dispose();
        }

        private void SampleLoop()
        {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    var current = CaptureSnapshot();
                    _peak = Max(_peak, current);
                    Interlocked.Increment(ref _sampleCount);
                }
                catch
                {
                    // A best-effort sample cannot invalidate deterministic scan assertions.
                }

                Thread.Sleep(2);
            }
        }
    }

    private sealed class CancellingTextReader(string content, CancellationTokenSource cancellation) : StringReader(content)
    {
        private int _readCount;

        public bool WasDisposed { get; private set; }

        public override int Read(char[] buffer, int index, int count)
        {
            var read = base.Read(buffer, index, count);
            if (read > 0 && Interlocked.Increment(ref _readCount) == 1)
                cancellation.Cancel();
            return read;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                WasDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class TrackingTextReader(string content) : StringReader(content)
    {
        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                WasDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed record FixtureFile(string RelativePath, string Content);

    private sealed record FixtureLayout(
        string Root,
        string StableRoot,
        IReadOnlyList<GrowthCase> GrowthCases,
        string ContextFile,
        string CancellationFile,
        string BudgetFile,
        IReadOnlyList<UserFixture> Users,
        string Digest,
        int StableRows,
        int CancellationContentBytes,
        int BudgetContentBytes);

    private sealed record GrowthCase(
        string Name,
        string FullPath,
        int ExpectedRows,
        int ContentBytes);

    private sealed record UserFixture(
        int Index,
        string Root,
        string Pattern,
        int ExpectedRows,
        ScopePolicy Scope);

    private sealed record RepeatedRun(
        string ResultHash,
        IReadOnlyList<RepeatedTrial> Trials);

    private sealed record RepeatedTrial(
        int Number,
        ScanObservation Scan,
        ResourceSnapshot AfterScan);

    private sealed record GrowthObservation(
        GrowthCase Fixture,
        ScanObservation Scan,
        ResourceSnapshot AfterScan);

    private sealed record EvidenceRun(
        string ResultHash,
        IReadOnlyList<EvidenceTrial> Trials);

    private sealed record EvidenceTrial(
        int Number,
        ScanObservation Scan,
        ResourceSnapshot AfterScan);

    private sealed record CacheRun(
        int PressuredPatternCount,
        CacheObservation AfterPressure,
        CacheObservation AfterConcurrent,
        long BeforeRecompile,
        CacheObservation AfterRecompile);

    private sealed record CacheObservation(
        int CachedPatternCount,
        long CompilationCount);

    private sealed record CancellationAttempt(
        int Number,
        bool CancellationObserved,
        bool ReaderDisposed,
        SearchTerminalSummary Summary,
        ResourceSnapshot AfterAttempt);

    private sealed record ConcurrentUserRun(
        UserFixture Fixture,
        IReadOnlyList<ConcurrentRound> Rounds);

    private sealed record ConcurrentRound(
        int Number,
        ScanObservation Scan);

    private sealed record ScanObservation(
        int Rows,
        string ResultHash,
        IReadOnlyList<string> Paths,
        IReadOnlyList<string?> MatchedTexts,
        int RowsWithContext,
        SearchTerminalSummary Summary);

    private readonly record struct RowIdentity(
        string Path,
        long MatchIndex,
        string? MatchText);

    private sealed record ResourceSnapshot(
        long ManagedHeapBytes,
        long TotalAllocatedBytes,
        long WorkingSetBytes,
        long PrivateMemoryBytes,
        long? HandleCount,
        int Gen2Collections);

    private sealed record ResourceWindow(
        double ElapsedMilliseconds,
        int SampleCount,
        ResourceSnapshot Start,
        ResourceSnapshot End,
        ResourceSnapshot Peak);

    private sealed record HandleCheck(
        long? Baseline,
        long? After,
        bool WithinSlack);
}

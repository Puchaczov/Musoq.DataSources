#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.Search.Tests.Benchmarks.Measurements;

[TestClass]
public sealed class SearchResourceStabilityResultsTests
{
    [TestMethod]
    public void Results_ShouldPinHarnessFixtureAndRawArtifacts()
    {
        using var results = Load("docs", "search", "search-resource-stability-results-v1.json");
        var root = results.RootElement;

        Assert.AreEqual("current_platform_observation", root.GetProperty("status").GetString());
        Assert.AreEqual("W16-S04", root.GetProperty("scopeId").GetString());
        Assert.AreEqual(
            root.GetProperty("contract").GetProperty("harnessSha256").GetString(),
            Sha256(PathFromRoot(root.GetProperty("contract").GetProperty("harnessPath").GetString()!)));
        Assert.AreEqual(
            root.GetProperty("contract").GetProperty("rawSha256").GetString(),
            Sha256(PathFromRoot(root.GetProperty("contract").GetProperty("rawPath").GetString()!)));

        foreach (var artifact in root.GetProperty("rawArtifacts").EnumerateArray())
        {
            var path = PathFromRoot(artifact.GetProperty("path").GetString()!);
            Assert.IsTrue(File.Exists(path), $"Missing raw artifact '{path}'.");
            Assert.AreEqual(
                artifact.GetProperty("sha256").GetString(),
                Sha256(path),
                $"Raw artifact hash changed for '{path}'.");
        }
    }

    [TestMethod]
    public void RepeatedAndGrowingWorkloads_ShouldRetainExactCompleteResults()
    {
        using var raw = Load(
            "docs",
            "campaigns",
            "search",
            "evidence",
            "W16-S04",
            "measure-resource-stability.json");
        var root = raw.RootElement;

        Assert.AreEqual("measure-resource-stability", root.GetProperty("command").GetString());
        Assert.AreEqual("W16-S04", root.GetProperty("scopeId").GetString());
        Assert.AreEqual(20, root.GetProperty("trialPolicy").GetProperty("repeatedIterations").GetInt32());
        Assert.AreEqual(8, root.GetProperty("trialPolicy").GetProperty("concurrentUsers").GetInt32());
        Assert.AreEqual(4, root.GetProperty("trialPolicy").GetProperty("concurrentRounds").GetInt32());

        var repeated = root.GetProperty("repeatedScans");
        var repeatedTrials = repeated.GetProperty("trials").EnumerateArray().ToArray();
        Assert.AreEqual(20, repeatedTrials.Length);
        Assert.AreEqual(1, repeatedTrials
            .Select(trial => trial.GetProperty("normalizedResultHash").GetString())
            .Distinct(StringComparer.Ordinal)
            .Count());
        Assert.IsTrue(repeatedTrials.All(trial =>
            trial.GetProperty("rows").GetInt32() == 4_096 &&
            trial.GetProperty("terminal").GetProperty("outcome").GetString() == "ScopeExhausted" &&
            trial.GetProperty("terminal").GetProperty("complete").GetBoolean() &&
            trial.GetProperty("terminal").GetProperty("countsExact").GetBoolean()));

        var growth = root.GetProperty("growingMatchSets").GetProperty("cases")
            .EnumerateArray()
            .ToArray();
        CollectionAssert.AreEqual(
            new[] { 1, 8, 64, 256 },
            growth.Select(item => item.GetProperty("expectedRows").GetInt32()).ToArray());
        Assert.IsTrue(growth.All(item =>
            item.GetProperty("rows").GetInt32() == item.GetProperty("expectedRows").GetInt32() &&
            item.GetProperty("terminal").GetProperty("outcome").GetString() == "ScopeExhausted" &&
            item.GetProperty("terminal").GetProperty("complete").GetBoolean()));
    }

    [TestMethod]
    public void CleanupCacheCancellationAndConcurrentWorkloads_ShouldPreserveContracts()
    {
        using var raw = Load(
            "docs",
            "campaigns",
            "search",
            "evidence",
            "W16-S04",
            "measure-resource-stability.json");
        var root = raw.RootElement;

        var evidence = root.GetProperty("retainedEvidence");
        Assert.AreEqual(6, evidence.GetProperty("iterations").GetInt32());
        Assert.IsTrue(evidence.GetProperty("everyTrialMaterializedContext").GetBoolean());
        Assert.IsTrue(evidence.GetProperty("readersReleasedBySourceFinally").GetBoolean());

        var cache = root.GetProperty("regexCachePressure");
        var maxCached = cache.GetProperty("maxCachedPatterns").GetInt32();
        Assert.AreEqual(128, maxCached);
        Assert.IsTrue(cache.GetProperty("bounded").GetBoolean());
        Assert.IsTrue(cache.GetProperty("evictionObserved").GetBoolean());
        Assert.IsTrue(cache.GetProperty("concurrentSameKeyCompilationSerialized").GetBoolean());
        Assert.IsTrue(cache.GetProperty("afterPressure").GetProperty("cachedPatternCount").GetInt32() <= maxCached);
        Assert.IsTrue(cache.GetProperty("afterConcurrentSharedKey").GetProperty("cachedPatternCount").GetInt32() <= maxCached);
        Assert.AreEqual(0, cache.GetProperty("afterReset").GetProperty("cachedPatternCount").GetInt32());
        Assert.AreEqual(0, cache.GetProperty("afterReset").GetProperty("compilationCount").GetInt64());
        Assert.AreEqual(
            cache.GetProperty("beforeEvictedKeyRecompile").GetInt64() + 1,
            cache.GetProperty("afterEvictedKeyRecompile").GetProperty("compilationCount").GetInt64());

        var cancellation = root.GetProperty("cancellation");
        var cancellationAttempts = cancellation.GetProperty("attempts").EnumerateArray().ToArray();
        Assert.AreEqual(8, cancellationAttempts.Length);
        Assert.IsTrue(cancellationAttempts.All(attempt =>
            attempt.GetProperty("cancellationObserved").GetBoolean() &&
            attempt.GetProperty("readerDisposed").GetBoolean() &&
            attempt.GetProperty("terminal").GetProperty("outcome").GetString() == "Failed" &&
            attempt.GetProperty("terminal").GetProperty("terminalReason").GetString() == "cancelled" &&
            attempt.GetProperty("terminal").GetProperty("failureCode").GetString() == "cancelled" &&
            !attempt.GetProperty("terminal").GetProperty("complete").GetBoolean()));

        var budget = root.GetProperty("resourceLimit");
        Assert.IsTrue(budget.GetProperty("enforced").GetBoolean());
        Assert.AreEqual("match-count", budget.GetProperty("exception").GetProperty("budgetCode").GetString());
        Assert.AreEqual("match-count", budget.GetProperty("terminal").GetProperty("failureCode").GetString());
        Assert.IsTrue(budget.GetProperty("readerDisposed").GetBoolean());
        Assert.IsFalse(budget.GetProperty("complete").GetBoolean());

        var concurrent = root.GetProperty("concurrentUsers");
        Assert.AreEqual(8, concurrent.GetProperty("userCount").GetInt32());
        Assert.AreEqual(8, concurrent.GetProperty("distinctScopeFingerprints").GetInt32());
        Assert.IsTrue(concurrent.GetProperty("allRowsStayedWithinUserPatternAndVisiblePath").GetBoolean());
        foreach (var user in concurrent.GetProperty("users").EnumerateArray())
        {
            var pattern = user.GetProperty("pattern").GetString();
            var rounds = user.GetProperty("rounds").EnumerateArray().ToArray();
            Assert.AreEqual(4, rounds.Length);
            Assert.IsTrue(rounds.All(round =>
                round.GetProperty("rows").GetInt32() == 3 &&
                round.GetProperty("paths").EnumerateArray().Single().GetString() == "visible.txt" &&
                round.GetProperty("matchedTexts").EnumerateArray().Single().GetString() == pattern &&
                round.GetProperty("terminal").GetProperty("outcome").GetString() == "ScopeExhausted"));
        }

        var cleanup = root.GetProperty("cleanup");
        Assert.IsTrue(cleanup.GetProperty("forcedCollections").GetBoolean());
        Assert.IsTrue(cleanup.GetProperty("handleGrowthWithinSlack").GetBoolean());
    }

    private static JsonDocument Load(params string[] parts)
    {
        return JsonDocument.Parse(File.ReadAllText(PathFromRoot(Path.Combine(parts))));
    }

    private static string PathFromRoot(string relativePath)
    {
        return Path.Combine(FindRepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string Sha256(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Musoq.DataSources.sln")))
                return directory.FullName;
        }

        throw new InvalidOperationException("Could not locate the datasource repository root.");
    }
}

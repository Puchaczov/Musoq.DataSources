#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.Search.Tests.Benchmarks.Measurements;

[TestClass]
public sealed class SearchDeliveryLatencyResultsTests
{
    [TestMethod]
    public void Results_ShouldPinContractBoundariesAndRawArtifacts()
    {
        using var results = Load("docs", "search", "search-delivery-latency-results-v1.json");
        var root = results.RootElement;

        Assert.AreEqual("current_platform_observed_with_external_residuals", root.GetProperty("status").GetString());
        Assert.AreEqual("W16-S03", root.GetProperty("scopeId").GetString());
        Assert.AreEqual(
            root.GetProperty("contract").GetProperty("harnessSha256").GetString(),
            Sha256(PathFromRoot(root.GetProperty("contract").GetProperty("harnessPath").GetString()!)));
        Assert.AreEqual(7, root.GetProperty("contract").GetProperty("minimumMeasuredTrialsPerCell").GetInt32());
        Assert.IsTrue(root.GetProperty("contract").GetProperty("streamingRule").GetString()!.Contains("must not", StringComparison.Ordinal));
        Assert.AreEqual(
            root.GetProperty("measurement").GetProperty("rawSha256").GetString(),
            Sha256(PathFromRoot(root.GetProperty("measurement").GetProperty("rawPath").GetString()!)));

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
    public void ObservedCells_ShouldHaveSevenCompleteTrialsAndOrderedBoundaries()
    {
        using var results = Load("docs", "search", "search-delivery-latency-results-v1.json");
        using var raw = Load("docs", "campaigns", "search", "evidence", "W16-S03", "measure-delivery-latency.json");
        var root = results.RootElement;
        var rawRoot = raw.RootElement;

        Assert.AreEqual(7, rawRoot.GetProperty("trialPolicy").GetProperty("measuredTrials").GetInt32());
        Assert.AreEqual(1, rawRoot.GetProperty("trialPolicy").GetProperty("warmupsExcluded").GetInt32());
        Assert.AreEqual(25, rawRoot.GetProperty("fixture").GetProperty("eligibleBytes").GetInt64());
        Assert.AreEqual(3, rawRoot.GetProperty("fixture").GetProperty("expectedRows").GetInt32());
        StringAssert.Contains(rawRoot.GetProperty("cachePolicy").GetString()!, "unknown");

        var cells = rawRoot.GetProperty("observedCells").EnumerateArray().ToArray();
        CollectionAssert.AreEquivalent(
            new[] { "direct-source-fresh", "compiled-query-compile-cache-miss", "compiled-query-reuse" },
            cells.Select(cell => cell.GetProperty("id").GetString()!).ToArray());
        var resultHashes = cells
            .SelectMany(cell => cell.GetProperty("trials").EnumerateArray())
            .Select(trial => trial.GetProperty("normalizedResultHash").GetString()!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.AreEqual(1, resultHashes.Length);
        Assert.AreEqual(64, resultHashes[0].Length);
        Assert.IsTrue(resultHashes[0].All(character => Uri.IsHexDigit(character)));
        Assert.AreEqual(
            resultHashes[0],
            root.GetProperty("measurement").GetProperty("normalizedResultHash").GetString());

        foreach (var cell in cells)
        {
            Assert.AreEqual("unknown", cell.GetProperty("filesystemCache").GetString());
            var trials = cell.GetProperty("trials").EnumerateArray().ToArray();
            Assert.AreEqual(7, trials.Length);
            CollectionAssert.AreEqual(
                Enumerable.Range(1, 7).ToArray(),
                trials.Select(trial => trial.GetProperty("trial").GetInt32()).OrderBy(number => number).ToArray());
            foreach (var trial in trials)
            {
                Assert.IsTrue(trial.GetProperty("complete").GetBoolean());
                Assert.AreEqual("complete", trial.GetProperty("terminalState").GetString());
                Assert.AreEqual(3, trial.GetProperty("rows").GetInt32());
                Assert.IsTrue(trial.GetProperty("terminalMilliseconds").GetDouble() >=
                              trial.GetProperty(cell.GetProperty("id").GetString() == "direct-source-fresh"
                                  ? "firstSourceChunkMilliseconds"
                                  : "firstClientVisibleResultMilliseconds").GetDouble());
                var id = cell.GetProperty("id").GetString();
                var postBoundary = trial.GetProperty(id == "direct-source-fresh"
                    ? "terminalAfterFirstSourceChunkMilliseconds"
                    : "firstClientVisibleResultAfterQueryReturnMilliseconds").GetDouble();
                Assert.IsTrue(postBoundary >= 0);
                if (id != "direct-source-fresh")
                    Assert.IsTrue(trial.GetProperty("terminalAfterFirstClientVisibleResultMilliseconds").GetDouble() >= 0);
            }
        }

        var descriptorCells = root.GetProperty("measurement").GetProperty("cells").EnumerateArray().ToArray();
        Assert.AreEqual(3, descriptorCells.Length);
        Assert.IsTrue(descriptorCells.All(cell => cell.GetProperty("trials").GetInt32() == 7));
        Assert.AreEqual("unsupported_claim", root.GetProperty("interpretation").GetProperty("streaming").GetString());
    }

    [TestMethod]
    public void ExternalDeliveryBoundaries_ShouldRemainUnmeasured()
    {
        using var results = Load("docs", "search", "search-delivery-latency-results-v1.json");
        var external = results.RootElement.GetProperty("externalBoundaries").EnumerateArray().ToArray();

        Assert.AreEqual(2, external.Length);
        Assert.IsTrue(external.All(boundary => boundary.GetProperty("status").GetString() == "not_run"));
        CollectionAssert.AreEquivalent(
            new[] { "cold-client-end-to-end", "warm-service-end-to-end" },
            external.Select(boundary => boundary.GetProperty("id").GetString()!).ToArray());
        StringAssert.Contains(
            results.RootElement.GetProperty("additiveLatencyBudget").GetProperty("coldClientEndToEnd").GetString()!,
            "not_measured");
        StringAssert.Contains(
            results.RootElement.GetProperty("additiveLatencyBudget").GetProperty("warmServiceEndToEnd").GetString()!,
            "not_measured");
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

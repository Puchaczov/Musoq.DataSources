#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.Search.Tests.Benchmarks.Agent;

[TestClass]
public sealed class SearchBenchmarkAgentReportTests
{
    [TestMethod]
    public void Report_ShouldPinEveryInputArtifact()
    {
        using var report = Load("docs", "search", "search-benchmark-agent-evaluation-results-v1.json");
        var root = report.RootElement;

        Assert.AreEqual("current_platform_published_with_explicit_residuals", root.GetProperty("status").GetString());
        Assert.AreEqual("W16-S05", root.GetProperty("scopeId").GetString());

        var inputs = root.GetProperty("inputArtifacts").EnumerateArray().ToArray();
        Assert.AreEqual(14, inputs.Length);
        Assert.AreEqual(inputs.Length, inputs.Select(input => input.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());
        foreach (var input in inputs)
        {
            var path = PathFromRoot(input.GetProperty("path").GetString()!);
            Assert.IsTrue(File.Exists(path), $"Missing report input '{path}'.");
            Assert.AreEqual(
                input.GetProperty("sha256").GetString(),
                Sha256(path),
                $"Report input hash changed for '{path}'.");
        }
    }

    [TestMethod]
    public void Report_ShouldRebuildMeasuredFactsFromPriorResults()
    {
        using var report = Load("docs", "search", "search-benchmark-agent-evaluation-results-v1.json");
        using var parity = Load("docs", "campaigns", "search", "evidence", "W16-S02", "report.json");
        using var literal = Load("docs", "campaigns", "search", "evidence", "W16-S02", "measure.json");
        using var batched = Load("docs", "campaigns", "search", "evidence", "W16-S02", "measure-batched.json");
        using var latency = Load("docs", "search", "search-delivery-latency-results-v1.json");
        using var resource = Load("docs", "search", "search-resource-stability-results-v1.json");

        var publishedParity = report.RootElement.GetProperty("measuredFacts").GetProperty("parity");
        var sourceParity = parity.RootElement.GetProperty("benchmark");
        Assert.AreEqual(sourceParity.GetProperty("comparableParityCohorts").GetInt32(), publishedParity.GetProperty("comparableParityCohorts").GetInt32());
        Assert.AreEqual(sourceParity.GetProperty("requiredParityCohorts").GetInt32(), publishedParity.GetProperty("requiredParityCohorts").GetInt32());
        Assert.AreEqual(sourceParity.GetProperty("comparableCells").GetInt32(), publishedParity.GetProperty("comparableCells").GetInt32());
        Assert.AreEqual(sourceParity.GetProperty("residualParityCohorts").GetInt32(), publishedParity.GetProperty("residualParityCohorts").GetInt32());
        Assert.AreEqual(
            sourceParity.GetProperty("ratioGate").GetProperty("geomeanCandidateToBaselineMedianRatio").GetDouble(),
            publishedParity.GetProperty("geomeanSearchToRipgrepMedianRatio").GetDouble(),
            0.0);
        Assert.AreEqual(
            sourceParity.GetProperty("ratioGate").GetProperty("maximumCellRatio").GetDouble(),
            publishedParity.GetProperty("maximumCellSearchToRipgrepMedianRatio").GetDouble(),
            0.0);
        Assert.AreEqual(6, literal.RootElement.GetProperty("cells").GetArrayLength());
        Assert.AreEqual(6, batched.RootElement.GetProperty("cells").GetArrayLength());

        var publishedLatency = report.RootElement.GetProperty("measuredFacts").GetProperty("latency");
        var sourceLatency = latency.RootElement.GetProperty("measurement").GetProperty("cells").EnumerateArray().ToArray();
        var latencyCells = publishedLatency.GetProperty("cells").EnumerateArray().ToArray();
        Assert.AreEqual(sourceLatency.Length, latencyCells.Length);
        for (var index = 0; index < sourceLatency.Length; index++)
        {
            Assert.AreEqual(sourceLatency[index].GetProperty("id").GetString(), latencyCells[index].GetProperty("id").GetString());
            Assert.AreEqual(sourceLatency[index].GetProperty("trials").GetInt32(), latencyCells[index].GetProperty("trials").GetInt32());
            Assert.AreEqual(
                sourceLatency[index].GetProperty("terminalMedianMilliseconds").GetDouble(),
                latencyCells[index].GetProperty("terminalMedianMilliseconds").GetDouble(),
                0.0);
        }
        CollectionAssert.AreEquivalent(
            new[] { "cold-client-end-to-end", "warm-service-end-to-end" },
            publishedLatency.GetProperty("externalBoundaries").EnumerateArray()
                .Select(boundary => boundary.GetProperty("id").GetString()!)
                .ToArray());

        var publishedResource = report.RootElement.GetProperty("measuredFacts").GetProperty("resourceStability");
        var sourceResource = resource.RootElement;
        Assert.AreEqual(sourceResource.GetProperty("workloads").GetProperty("repeatedScans").GetProperty("iterations").GetInt32(), publishedResource.GetProperty("repeatedIterations").GetInt32());
        Assert.AreEqual(sourceResource.GetProperty("workloads").GetProperty("repeatedScans").GetProperty("expectedRows").GetInt32(), publishedResource.GetProperty("repeatedExpectedRows").GetInt32());
        CollectionAssert.AreEqual(
            new[] { 1, 8, 64, 256 },
            publishedResource.GetProperty("growingExpectedRows").EnumerateArray().Select(value => value.GetInt32()).ToArray());
        Assert.AreEqual(sourceResource.GetProperty("resourceObservation").GetProperty("baselineHandleCount").GetInt32(), publishedResource.GetProperty("baselineHandleCount").GetInt32());
        Assert.AreEqual(sourceResource.GetProperty("resourceObservation").GetProperty("afterCollectionHandleCount").GetInt32(), publishedResource.GetProperty("afterCollectionHandleCount").GetInt32());
        Assert.IsTrue(publishedResource.GetProperty("handleGrowthWithinSlack").GetBoolean());
    }

    [TestMethod]
    public void Report_ShouldKeepTargetsAndAgentResultsSeparate()
    {
        using var report = Load("docs", "search", "search-benchmark-agent-evaluation-results-v1.json");
        using var neutral = Load("docs", "search", "search-neutral-choice-evaluation-v1.json");
        using var holdout = Load("docs", "search", "search-holdout-freeze-v1.json");
        using var friction = Load("docs", "search", "search-agent-friction-regressions-v1.json");
        var root = report.RootElement;

        Assert.AreEqual("proposed_target_not_measurement", root.GetProperty("targets").GetProperty("performanceParity").GetProperty("status").GetString());
        Assert.AreEqual("proposed_target_not_measurement", root.GetProperty("targets").GetProperty("latency").GetProperty("status").GetString());
        Assert.AreEqual("proposed_target_not_measured", root.GetProperty("targets").GetProperty("agentUsability").GetProperty("status").GetString());

        var publishedNeutral = root.GetProperty("agentEvaluation").GetProperty("neutralChoice");
        Assert.AreEqual(neutral.RootElement.GetProperty("evaluationContract").GetProperty("requiredTrialCount").GetInt32(), publishedNeutral.GetProperty("requiredTrials").GetInt32());
        Assert.AreEqual(0, publishedNeutral.GetProperty("completedTrials").GetInt32());
        Assert.IsFalse(publishedNeutral.GetProperty("resultsAvailable").GetBoolean());

        var publishedHoldout = root.GetProperty("agentEvaluation").GetProperty("holdout");
        var sourceHoldout = holdout.RootElement.GetProperty("holdout");
        Assert.AreEqual(sourceHoldout.GetProperty("taskIds").GetArrayLength(), publishedHoldout.GetProperty("taskCount").GetInt32());
        Assert.AreEqual(sourceHoldout.GetProperty("minimumTrialsPerTask").GetInt32(), publishedHoldout.GetProperty("minimumTrialsPerTask").GetInt32());
        Assert.AreEqual(0, publishedHoldout.GetProperty("completedTrials").GetInt32());
        Assert.AreEqual(0, publishedHoldout.GetProperty("resultRecords").GetInt32());
        Assert.IsFalse(publishedHoldout.GetProperty("answersStoredInRepository").GetBoolean());

        var publishedFriction = root.GetProperty("agentEvaluation").GetProperty("frictionRegressions");
        Assert.AreEqual(friction.RootElement.GetProperty("regressions").GetArrayLength(), publishedFriction.GetProperty("regressionCount").GetInt32());
        Assert.AreEqual(friction.RootElement.GetProperty("observations").GetArrayLength(), publishedFriction.GetProperty("observations").GetInt32());
        StringAssert.Contains(root.GetProperty("claimPolicy").GetProperty("speedClaim").GetString()!, "No blanket faster-than-rg");
        StringAssert.Contains(root.GetProperty("agentEvaluation").GetProperty("runnerStatus").GetString()!, "No fresh-context agent runner");
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

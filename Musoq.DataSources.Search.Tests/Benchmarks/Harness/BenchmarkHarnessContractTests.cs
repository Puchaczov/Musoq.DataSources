#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Search.Benchmarks.Harness;

namespace Musoq.DataSources.Search.Tests.Benchmarks.Harness;

[TestClass]
public sealed class BenchmarkHarnessContractTests
{
    [TestMethod]
    public void FrozenHarness_ShouldAcceptACompleteEquivalentCell()
    {
        var cell = CreateValidCell();

        BenchmarkHarnessContract.Validate(cell, "W03-S05", "occurrence");
    }

    [TestMethod]
    public void FrozenHarness_ShouldRejectMismatchedScopeAndOutputUnit()
    {
        var wrongScope = CreateValidCell() with { ScopeId = "W03-S04" };
        var wrongUnit = CreateValidCell() with { ResultUnit = "matching_line" };

        AssertContractError("scope-mismatch", () =>
            BenchmarkHarnessContract.Validate(wrongScope, "W03-S05", "occurrence"));
        AssertContractError("output-unit-mismatch", () =>
            BenchmarkHarnessContract.Validate(wrongUnit, "W03-S05", "occurrence"));
    }

    [TestMethod]
    public void FrozenHarness_ShouldRejectMissingBaseline()
    {
        var cell = CreateValidCell() with { Baseline = null };

        AssertContractError("missing-baseline", () =>
            BenchmarkHarnessContract.Validate(cell, "W03-S05", "occurrence"));
    }

    [TestMethod]
    public void FrozenHarness_ShouldRejectZeroByteScansEvenWhenTheResultIsEmpty()
    {
        var cell = CreateValidCell() with { EligibleBytes = 0 };

        AssertContractError("zero-byte-scan", () =>
            BenchmarkHarnessContract.Validate(cell, "W03-S05", "occurrence"));
    }

    [TestMethod]
    public void FrozenHarness_ShouldRejectIncompleteRunsAndUnknownColdClaims()
    {
        var incomplete = CreateValidCell() with
        {
            Complete = false,
            TerminalState = "cancelled"
        };
        var unknownCold = CreateValidCell() with
        {
            ProcessState = "cold-client",
            FilesystemCache = "unknown"
        };

        AssertContractError("incomplete-run", () =>
            BenchmarkHarnessContract.Validate(incomplete, "W03-S05", "occurrence"));
        AssertContractError("unknown-cache-labeled-cold", () =>
            BenchmarkHarnessContract.Validate(unknownCold, "W03-S05", "occurrence"));
    }

    [TestMethod]
    public void FrozenHarness_ShouldRejectMismatchedResultsAndIncompleteTrials()
    {
        var mismatched = CreateValidCell() with
        {
            Candidate = CreateSnapshot(
                "managed",
                resultHash: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
        };
        var trials = CreateTrials();
        trials[0] = new BenchmarkTrial(1, "baseline,candidate", 1, 1, false, true);
        var incompleteTrial = CreateValidCell() with { Trials = trials };

        AssertContractError("output-mismatch", () =>
            BenchmarkHarnessContract.Validate(mismatched, "W03-S05", "occurrence"));
        AssertContractError("incomplete-trial", () =>
            BenchmarkHarnessContract.Validate(incompleteTrial, "W03-S05", "occurrence"));
    }

    [TestMethod]
    public void FrozenContractDocument_ShouldDeclareTheRequiredLayersCohortsAndThresholds()
    {
        var path = FindRepositoryFile("docs", "search", "search-benchmark-harness-v1.json");
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        Assert.AreEqual("frozen", root.GetProperty("status").GetString());
        Assert.AreEqual(7, root.GetProperty("trialPolicy").GetProperty("minimumMeasuredTrials").GetInt32());
        CollectionAssert.AreEquivalent(
            new[]
            {
                "matching_kernel_equivalent_in_process_sink",
                "datasource_chunks",
                "compiled_query",
                "warm_service_end_to_end",
                "cold_client_end_to_end"
            },
            root.GetProperty("measurementLayers")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());
        Assert.AreEqual(24, root.GetProperty("requiredCohorts").GetArrayLength());
        Assert.AreEqual(1.25, root.GetProperty("thresholds").GetProperty("parityGeomeanRatioMax").GetDouble(), 0.00001);
        Assert.AreEqual(2.0, root.GetProperty("thresholds").GetProperty("perCellMedianRatioMax").GetDouble(), 0.00001);
    }

    private static BenchmarkCell CreateValidCell()
    {
        const long eligibleBytes = 1234;
        const string outputHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        return new BenchmarkCell(
            "W03-S05",
            "B03",
            "sparse-literal",
            "occurrence",
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            eligibleBytes,
            "reference",
            "managed",
            "fresh-process",
            "controlled_warm",
            1,
            "Includes traversal, file open/read, decode, matching, bridge setup and output serialization/hash.",
            CreateTrials(),
            true,
            "complete",
            CreateSnapshot("reference", eligibleBytes, outputHash),
            CreateSnapshot("managed", eligibleBytes, outputHash));
    }

    private static BenchmarkTrial[] CreateTrials()
    {
        return Enumerable.Range(1, BenchmarkHarnessContract.MinimumMeasuredTrials)
            .Select(index => new BenchmarkTrial(
                index,
                index % 2 == 0 ? "candidate,baseline" : "baseline,candidate",
                2.0 + index,
                1.0 + index,
                true,
                true))
            .ToArray();
    }

    private static BenchmarkCandidateSnapshot CreateSnapshot(
        string id,
        long eligibleBytes = 1234,
        string resultHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")
    {
        return new BenchmarkCandidateSnapshot(
            id,
            id == "reference" ? "reference build" : ".NET 10.0.11",
            eligibleBytes,
            "occurrence",
            17,
            resultHash,
            true,
            "complete",
            1);
    }

    private static void AssertContractError(string code, Action action)
    {
        var exception = Assert.ThrowsException<BenchmarkContractException>(action);
        Assert.AreEqual(code, exception.Code);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var path = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException(
            $"Unable to locate repository file '{Path.Combine(parts)}'.");
    }
}

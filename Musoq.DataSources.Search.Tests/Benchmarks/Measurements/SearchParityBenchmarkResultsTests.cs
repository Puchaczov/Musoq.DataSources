#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.Search.Tests.Benchmarks.Measurements;

[TestClass]
public sealed class SearchParityBenchmarkResultsTests
{
    [TestMethod]
    public void Results_ShouldPinFrozenContractsAndAccountForEveryParityCohort()
    {
        using var results = Load("docs", "search", "search-parity-benchmark-results-v1.json");
        using var matrix = Load("docs", "campaigns", "search", "benchmark-matrix.json");
        var root = results.RootElement;

        Assert.AreEqual("current_platform_observed_with_residuals", root.GetProperty("status").GetString());
        Assert.AreEqual("W16-S02", root.GetProperty("scopeId").GetString());
        Assert.AreEqual(
            root.GetProperty("contract").GetProperty("harnessSha256").GetString(),
            Sha256(PathFromRoot(root.GetProperty("contract").GetProperty("harnessPath").GetString()!)));
        Assert.AreEqual(
            root.GetProperty("contract").GetProperty("matrixSha256").GetString(),
            Sha256(PathFromRoot(root.GetProperty("contract").GetProperty("matrixPath").GetString()!)));

        var expectedCohorts = matrix.RootElement
            .GetProperty("cohorts")
            .EnumerateArray()
            .Where(cohort => cohort.GetProperty("parityCohort").GetBoolean())
            .Select(cohort => cohort.GetProperty("id").GetString()!)
            .ToArray();
        var inventory = root.GetProperty("cohortInventory").EnumerateArray().ToArray();
        CollectionAssert.AreEquivalent(
            matrix.RootElement.GetProperty("cohorts")
                .EnumerateArray()
                .Select(cohort => cohort.GetProperty("id").GetString()!)
                .ToArray(),
            inventory.Select(cohort => cohort.GetProperty("id").GetString()!).ToArray());
        CollectionAssert.AreEquivalent(
            expectedCohorts,
            inventory
                .Where(cohort => cohort.GetProperty("parityCohort").GetBoolean())
                .Select(cohort => cohort.GetProperty("id").GetString()!)
                .ToArray());

        var measured = inventory
            .Where(cohort => cohort.GetProperty("status").GetString() == "measured")
            .Select(cohort => cohort.GetProperty("id").GetString()!)
            .ToArray();
        CollectionAssert.AreEquivalent(new[] { "B02", "B03", "B04", "B11" }, measured);
        Assert.AreEqual(9, inventory.Count(cohort =>
            cohort.GetProperty("parityCohort").GetBoolean() &&
            cohort.GetProperty("status").GetString() != "measured"));

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
    public void PairedResults_ShouldHaveSevenCompleteTrialsEqualOutputsAndPassingRatios()
    {
        using var results = Load("docs", "search", "search-parity-benchmark-results-v1.json");
        var root = results.RootElement;

        using var literal = Load("docs", "campaigns", "search", "evidence", "W16-S02", "measure.json");
        var literalCells = literal.RootElement.GetProperty("cells").EnumerateArray().ToArray();
        Assert.AreEqual(6, literalCells.Length);
        foreach (var cell in literalCells)
        {
            Assert.AreEqual("unknown", cell.GetProperty("FilesystemCache").GetString());
            Assert.IsTrue(cell.GetProperty("ExpectedOccurrences").GetInt32() >= 0);
            var samples = cell.GetProperty("Samples").EnumerateArray().ToArray();
            Assert.AreEqual(14, samples.Length);
            for (var trialNumber = 1; trialNumber <= 7; trialNumber++)
            {
                var trial = samples
                    .Where(sample => sample.GetProperty("Trial").GetInt32() == trialNumber)
                    .ToArray();
                Assert.AreEqual(2, trial.Length);
                var baseline = trial.Single(sample => sample.GetProperty("Candidate").GetString() == "ripgrep");
                var candidate = trial.Single(sample => sample.GetProperty("Candidate").GetString() == "managed");
                Assert.IsTrue(baseline.GetProperty("DurationMilliseconds").GetDouble() > 0);
                Assert.IsTrue(candidate.GetProperty("DurationMilliseconds").GetDouble() > 0);
                Assert.AreEqual(baseline.GetProperty("BytesRead").GetInt64(), candidate.GetProperty("BytesRead").GetInt64());
                Assert.IsTrue(candidate.GetProperty("BytesRead").GetInt64() > 0);
                Assert.AreEqual(baseline.GetProperty("OutputHash").GetString(), candidate.GetProperty("OutputHash").GetString());
                Assert.AreEqual(JsonValueKind.Null, candidate.GetProperty("ExitCode").ValueKind);
                var baselineExit = baseline.GetProperty("ExitCode").GetInt32();
                Assert.IsTrue(baselineExit is 0 or 1, $"Unexpected ripgrep exit code {baselineExit}.");
            }
        }

        using var batched = Load("docs", "campaigns", "search", "evidence", "W16-S02", "measure-batched.json");
        var batchedCells = batched.RootElement.GetProperty("cells").EnumerateArray().ToArray();
        Assert.AreEqual(6, batchedCells.Length);
        foreach (var cell in batchedCells)
        {
            Assert.IsTrue(cell.GetProperty("eligibleBytes").GetInt64() > 0);
            var trials = cell.GetProperty("trials").EnumerateArray().ToArray();
            Assert.AreEqual(7, trials.Length);
            foreach (var trial in trials)
            {
                var baseline = trial.GetProperty("baseline");
                var candidate = trial.GetProperty("candidate");
                Assert.IsTrue(baseline.GetProperty("complete").GetBoolean());
                Assert.IsTrue(candidate.GetProperty("complete").GetBoolean());
                Assert.AreEqual("complete", baseline.GetProperty("terminalState").GetString());
                Assert.AreEqual("complete", candidate.GetProperty("terminalState").GetString());
                Assert.AreEqual(baseline.GetProperty("scannedBytes").GetInt64(), candidate.GetProperty("scannedBytes").GetInt64());
                Assert.IsTrue(candidate.GetProperty("scannedBytes").GetInt64() > 0);
                Assert.AreEqual(baseline.GetProperty("resultCount").GetInt32(), candidate.GetProperty("resultCount").GetInt32());
                Assert.AreEqual(baseline.GetProperty("outputHash").GetString(), candidate.GetProperty("outputHash").GetString());
                Assert.AreEqual(JsonValueKind.Null, candidate.GetProperty("exitCode").ValueKind);
                var baselineExit = baseline.GetProperty("exitCode").GetInt32();
                Assert.IsTrue(baselineExit is 0 or 1, $"Unexpected ripgrep exit code {baselineExit}.");
            }
        }

        var ratioGate = root.GetProperty("ratioGate");
        Assert.AreEqual(12, ratioGate.GetProperty("applicableCellCount").GetInt32());
        Assert.IsTrue(ratioGate.GetProperty("geomeanCandidateToBaselineMedianRatio").GetDouble() <= 1.25);
        Assert.IsTrue(ratioGate.GetProperty("maximumCellRatio").GetDouble() <= 2.0);
        Assert.AreEqual("passed_observed_cells_only", ratioGate.GetProperty("status").GetString());
    }

    [TestMethod]
    public void CandidateOnlyDiagnostics_ShouldRemainSeparateFromParityClaims()
    {
        using var results = Load("docs", "search", "search-parity-benchmark-results-v1.json");
        using var regex = Load("docs", "campaigns", "search", "evidence", "W16-S02", "measure-regex.json");
        using var decoding = Load("docs", "campaigns", "search", "evidence", "W16-S02", "measure-decoding.json");

        var diagnostics = results.RootElement.GetProperty("supportingDiagnostics").EnumerateArray().ToArray();
        Assert.AreEqual(2, diagnostics.Length);
        Assert.IsTrue(diagnostics.All(diagnostic =>
            diagnostic.GetProperty("status").GetString() == "candidate_only_not_comparable"));
        Assert.IsTrue(regex.RootElement.GetProperty("Passed").GetBoolean());
        Assert.AreEqual(7, regex.RootElement.GetProperty("TrialsPerPhase").GetInt32());
        Assert.IsTrue(regex.RootElement.GetProperty("Workloads").EnumerateArray()
            .All(workload => workload.GetProperty("Phases").EnumerateArray()
                .All(phase => phase.GetProperty("Trials").GetArrayLength() == 7)));
        Assert.AreEqual(7, decoding.RootElement.GetProperty("trialPolicy").GetProperty("measuredTrials").GetInt32());
        Assert.AreEqual(3, decoding.RootElement.GetProperty("cohorts").GetArrayLength());
        Assert.IsTrue(decoding.RootElement.GetProperty("cohorts").EnumerateArray()
            .All(cohort => cohort.GetProperty("trials").GetArrayLength() == 7));
        StringAssert.Contains(
            decoding.RootElement.GetProperty("trialPolicy").GetProperty("order").GetString()!,
            "no external baseline");
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

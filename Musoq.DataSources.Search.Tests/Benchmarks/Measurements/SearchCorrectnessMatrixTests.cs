#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.Search.Tests.Benchmarks.Measurements;

[TestClass]
public sealed class SearchCorrectnessMatrixTests
{
    [TestMethod]
    public void Matrix_ShouldInventoryEveryRequiredCorrectnessProfileWithoutSilentPlatformSkips()
    {
        using var matrix = Load("docs", "search", "search-correctness-matrix-v1.json");
        var root = matrix.RootElement;
        Assert.AreEqual("current_platform_executed_with_residuals", root.GetProperty("status").GetString());
        Assert.AreEqual("W16-S01", root.GetProperty("scopeId").GetString());
        Assert.IsTrue(root.GetProperty("execution").GetProperty("currentPlatform").GetString() == "windows-x64");

        var suites = root.GetProperty("suites").EnumerateArray().ToArray();
        var suiteIds = suites.Select(suite => suite.GetProperty("id").GetString()!).ToArray();
        CollectionAssert.AreEquivalent(
            new[] { "oracle", "property", "differential", "fuzz", "planner", "integration", "package-source-engine-compatibility" },
            suiteIds);
        foreach (var suite in suites)
        {
            Assert.IsTrue(suite.GetProperty("tests").GetArrayLength() > 0, $"Suite {suite.GetProperty("id").GetString()} has no mapped tests.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(suite.GetProperty("evidence").GetString()));
        }

        Assert.AreEqual(24, root.GetProperty("benchmarkMatrix").GetProperty("requiredCohortCount").GetInt32());
        Assert.IsFalse(root.GetProperty("benchmarkMatrix").GetProperty("resultsClaimed").GetBoolean());
        Assert.IsTrue(root.GetProperty("resultSummary").GetProperty("mandatoryCurrentPlatformSuitesPassed").GetBoolean());
    }

    [TestMethod]
    public void Matrix_ShouldMarkUnavailablePlatformAndFuzzClaimsAsResiduals()
    {
        using var matrix = Load("docs", "search", "search-correctness-matrix-v1.json");
        var profiles = matrix.RootElement.GetProperty("platformProfiles").EnumerateArray().ToArray();
        Assert.AreEqual(4, profiles.Length);
        Assert.AreEqual(1, profiles.Count(profile => profile.GetProperty("status").GetString() == "passed"));
        Assert.AreEqual(3, profiles.Count(profile => profile.TryGetProperty("claim", out var claim) && claim.GetString() == "blocked_unavailable"));
        Assert.AreEqual("residual_seeded_random_only", matrix.RootElement.GetProperty("suites").EnumerateArray()
            .Single(suite => suite.GetProperty("id").GetString() == "fuzz").GetProperty("status").GetString());
        Assert.IsFalse(matrix.RootElement.GetProperty("resultSummary").GetProperty("fullFuzzEngineAvailable").GetBoolean());
    }

    [TestMethod]
    public void Matrix_ShouldPinTheBenchmarkMatrixBytes()
    {
        using var matrix = Load("docs", "search", "search-correctness-matrix-v1.json");
        var descriptor = matrix.RootElement.GetProperty("benchmarkMatrix");
        var path = Path.Combine(FindRepositoryRoot(), descriptor.GetProperty("path").GetString()!.Replace('/', Path.DirectorySeparatorChar));
        Assert.IsTrue(File.Exists(path));
        var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        Assert.AreEqual(descriptor.GetProperty("sha256").GetString(), actual);
    }

    private static JsonDocument Load(params string[] parts)
    {
        return JsonDocument.Parse(File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. parts])));
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

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.Search.Tests.Benchmarks.Agent;

[TestClass]
public sealed class SearchAgentFrictionRegressionTests
{
    private static readonly string[] BlueprintIds =
        Enumerable.Range(1, 40).Select(index => $"A{index:00}").ToArray();

    [TestMethod]
    public void FrictionCatalog_ShouldReferenceKnownFirstAttemptsWithoutClaimingObservations()
    {
        using var catalog = Load("docs", "search", "search-agent-friction-regressions-v1.json");
        using var taskSet = Load("docs", "search", "search-first-success-task-set-v1.json");
        var root = catalog.RootElement;
        Assert.AreEqual("contract_regressions_not_empirical", root.GetProperty("status").GetString());
        Assert.AreEqual(0, root.GetProperty("observations").GetArrayLength());

        var tasks = taskSet.RootElement.GetProperty("tasks").EnumerateArray().ToDictionary(
            task => task.GetProperty("id").GetString()!, StringComparer.Ordinal);
        var regressions = root.GetProperty("regressions").EnumerateArray().ToArray();
        Assert.AreEqual(12, regressions.Length);
        Assert.AreEqual(regressions.Length, regressions.Select(item => item.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());
        foreach (var regression in regressions)
        {
            var taskId = regression.GetProperty("taskId").GetString()!;
            Assert.IsTrue(tasks.ContainsKey(taskId), $"Unknown task {taskId}.");
            Assert.IsTrue(tasks[taskId].TryGetProperty("firstAttempt", out _));
            Assert.IsFalse(regression.GetProperty("observed").GetBoolean());
            Assert.IsTrue(regression.GetProperty("requiredEvidence").GetArrayLength() > 0);
        }

        var categories = regressions.Select(item => tasks[item.GetProperty("taskId").GetString()!].GetProperty("category").GetString()!)
            .Distinct(StringComparer.Ordinal).ToArray();
        CollectionAssert.IsSubsetOf(new[] { "simple", "repair", "failure", "composition", "precision", "security" }, categories);
    }

    [TestMethod]
    public void HoldoutFreeze_ShouldContainOnlyBoundaryMetadataAndNoAnswers()
    {
        using var holdout = Load("docs", "search", "search-holdout-freeze-v1.json");
        var root = holdout.RootElement;
        Assert.AreEqual("frozen_contract_not_materialized", root.GetProperty("status").GetString());
        var taskIds = root.GetProperty("holdout").GetProperty("taskIds").EnumerateArray()
            .Select(task => task.GetString()!).ToArray();
        CollectionAssert.AreEqual(BlueprintIds, taskIds);
        Assert.AreEqual(3, root.GetProperty("holdout").GetProperty("minimumTrialsPerTask").GetInt32());
        Assert.IsTrue(root.GetProperty("holdout").GetProperty("freshContextPerTrial").GetBoolean());
        Assert.AreEqual(0, root.GetProperty("holdout").GetProperty("resultRecords").GetArrayLength());

        var serialized = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "search", "search-holdout-freeze-v1.json"));
        Assert.IsFalse(serialized.Contains("expectedAnswer", StringComparison.Ordinal));
        Assert.IsFalse(serialized.Contains("truthCounts", StringComparison.Ordinal));
        Assert.IsFalse(serialized.Contains("truthPaths", StringComparison.Ordinal));
        var commitment = root.GetProperty("holdout").GetProperty("seedCommitment").GetString()!;
        Assert.AreEqual(64, commitment.Length);
        Assert.IsTrue(commitment.All(Uri.IsHexDigit));
    }

    [TestMethod]
    public void HoldoutFreeze_ShouldPinTheBenchmarkAndDevelopmentProtocolBytes()
    {
        using var holdout = Load("docs", "search", "search-holdout-freeze-v1.json");
        AssertFileHash(holdout.RootElement.GetProperty("sourceBlueprint"));
        AssertFileHash(holdout.RootElement.GetProperty("developmentProtocol"));
    }

    private static void AssertFileHash(JsonElement descriptor)
    {
        var path = Path.Combine(FindRepositoryRoot(), descriptor.GetProperty("path").GetString()!.Replace('/', Path.DirectorySeparatorChar));
        Assert.IsTrue(File.Exists(path), $"Pinned source is missing: {path}");
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

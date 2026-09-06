#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.Search.Tests.Benchmarks.Agent;

[TestClass]
public sealed class SearchNeutralChoiceEvaluationTests
{
    private static readonly string[] ArmIds = ["search", "rg", "ordinary"];

    [TestMethod]
    public void Protocol_ShouldBeBalancedAndMustNotForceSearch()
    {
        using var protocol = Load("docs", "search", "search-neutral-choice-evaluation-v1.json");
        var root = protocol.RootElement;

        Assert.AreEqual("protocol_ready_not_executed", root.GetProperty("status").GetString());
        Assert.AreEqual("W15-S04", root.GetProperty("scopeId").GetString());
        Assert.IsFalse(root.GetProperty("sourceTaskSet").GetProperty("expectedAnswersCopied").GetBoolean());
        Assert.AreEqual(3, root.GetProperty("evaluationContract").GetProperty("minimumTrialsPerTask").GetInt32());
        Assert.AreEqual(45, root.GetProperty("evaluationContract").GetProperty("requiredTrialCount").GetInt32());

        var arms = root.GetProperty("evaluationContract").GetProperty("arms")
            .EnumerateArray().Select(arm => arm.GetProperty("id").GetString()!).ToArray();
        CollectionAssert.AreEqual(ArmIds, arms);
        foreach (var arm in root.GetProperty("evaluationContract").GetProperty("arms").EnumerateArray())
        {
            var description = arm.GetProperty("description").GetString()!;
            Assert.IsFalse(description.Contains("must use", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(description.Contains("required", StringComparison.OrdinalIgnoreCase));
        }

        var selected = root.GetProperty("selectedTasks").EnumerateArray().ToArray();
        Assert.AreEqual(15, selected.Length);
        CollectionAssert.AreEqual(
            new[] { "compositional", "cost-trap", "resource-boundary", "semantic-boundary", "simple-grep" },
            selected.Select(task => task.GetProperty("evaluationCategory").GetString()!).Distinct().OrderBy(value => value).ToArray());
        foreach (var category in selected.GroupBy(task => task.GetProperty("evaluationCategory").GetString()!))
            Assert.AreEqual(3, category.Count(), $"Category {category.Key} must have three tasks.");

        Assert.IsTrue(selected.Any(task => task.GetProperty("rgWinEligible").GetBoolean()));
        Assert.IsTrue(selected.Any(task => task.GetProperty("acceptedTools").EnumerateArray().Any(tool => tool.GetString() == "search")));
        Assert.IsTrue(selected.Any(task => task.GetProperty("acceptedTools").EnumerateArray().Any(tool => tool.GetString() == "ordinary")));
        Assert.IsTrue(selected.All(task => task.GetProperty("acceptedTools").EnumerateArray().All(tool => ArmIds.Contains(tool.GetString()))));
    }

    [TestMethod]
    public void Protocol_ShouldReferenceTheExecutedFirstSuccessTaskSetWithoutLeakingAnswers()
    {
        using var protocol = Load("docs", "search", "search-neutral-choice-evaluation-v1.json");
        using var taskSet = Load("docs", "search", "search-first-success-task-set-v1.json");
        var protocolRoot = protocol.RootElement;
        var source = protocolRoot.GetProperty("sourceTaskSet");
        var sourcePath = Path.Combine(FindRepositoryRoot(), source.GetProperty("path").GetString()!.Replace('/', Path.DirectorySeparatorChar));
        Assert.IsTrue(File.Exists(sourcePath));
        Assert.AreEqual(source.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath))).ToLowerInvariant());
        Assert.AreEqual(40, taskSet.RootElement.GetProperty("tasks").GetArrayLength());

        foreach (var task in protocolRoot.GetProperty("selectedTasks").EnumerateArray())
        {
            var taskId = task.GetProperty("taskId").GetString()!;
            var sourceTask = taskSet.RootElement.GetProperty("tasks").EnumerateArray()
                .Single(candidate => candidate.GetProperty("id").GetString() == taskId);
            Assert.IsFalse(task.TryGetProperty("expectedAnswer", out _));
            Assert.IsFalse(task.TryGetProperty("prompt", out _));
            Assert.IsFalse(sourceTask.GetProperty("results").GetArrayLength() > 0);
        }
    }

    [TestMethod]
    public void Protocol_ShouldRequireCompleteFailureAwareTrialRecords()
    {
        using var protocol = Load("docs", "search", "search-neutral-choice-evaluation-v1.json");
        var fields = protocol.RootElement.GetProperty("trialRecord").GetProperty("requiredFields")
            .EnumerateArray().Select(field => field.GetString()!).ToHashSet(StringComparer.Ordinal);

        foreach (var required in new[]
        {
            "taskId", "trial", "modelVersion", "toolOrder", "selectedTool", "firstValidAttempt", "repairAttempts",
            "toolCalls", "inputTokens", "outputTokens", "wallTimeMs", "sourceBytesRead", "answerCorrect",
            "evidenceValid", "completenessAware", "inappropriateToolSelection", "unsafeActionAttempts",
            "userInterventions", "terminalOutcome"
        })
            Assert.IsTrue(fields.Contains(required), $"Trial field '{required}' is missing.");

        Assert.AreEqual(0, protocol.RootElement.GetProperty("results").GetArrayLength());
        Assert.AreEqual("not_executed", protocol.RootElement.GetProperty("resultSummary").GetProperty("status").GetString());
        Assert.IsFalse(protocol.RootElement.GetProperty("resultSummary").GetProperty("agentResultsAvailable").GetBoolean());
    }

    [TestMethod]
    public void Wilson95_ShouldReturnFiniteIntervalsAndRetainZeroTrialNull()
    {
        Assert.IsNull(Wilson95(0, 0));
        var allCorrect = Wilson95(10, 10);
        Assert.IsNotNull(allCorrect);
        Assert.IsTrue(allCorrect!.Value.Lower > 0.7 && allCorrect.Value.Lower < 0.8);
        Assert.AreEqual(1.0, allCorrect.Value.Upper, 0.000001);

        var halfCorrect = Wilson95(5, 10);
        Assert.IsNotNull(halfCorrect);
        Assert.AreEqual(0.236593, halfCorrect!.Value.Lower, 0.000001);
        Assert.AreEqual(0.763407, halfCorrect.Value.Upper, 0.000001);
    }

    [TestMethod]
    public void ChoiceScore_ShouldSeparateAnswerValidityFromToolAppropriateness()
    {
        Assert.IsTrue(IsChoiceCorrect(answerCorrect: true, evidenceValid: true, completenessRequired: true, completenessAware: true, selectedTool: "rg", acceptedTools: ["rg", "ordinary"], unsafeActionAttempts: 0));
        Assert.IsFalse(IsChoiceCorrect(answerCorrect: true, evidenceValid: true, completenessRequired: true, completenessAware: false, selectedTool: "rg", acceptedTools: ["rg", "ordinary"], unsafeActionAttempts: 0));
        Assert.IsFalse(IsChoiceCorrect(answerCorrect: true, evidenceValid: true, completenessRequired: false, completenessAware: false, selectedTool: "search", acceptedTools: ["rg", "ordinary"], unsafeActionAttempts: 0));
        Assert.IsFalse(IsChoiceCorrect(answerCorrect: true, evidenceValid: true, completenessRequired: false, completenessAware: false, selectedTool: "ordinary", acceptedTools: ["ordinary"], unsafeActionAttempts: 1));
    }

    private static bool IsChoiceCorrect(bool answerCorrect, bool evidenceValid, bool completenessRequired, bool completenessAware, string selectedTool, IReadOnlyCollection<string> acceptedTools, int unsafeActionAttempts)
    {
        return answerCorrect && evidenceValid && (!completenessRequired || completenessAware) && acceptedTools.Contains(selectedTool) && unsafeActionAttempts == 0;
    }

    private static (double Lower, double Upper)? Wilson95(int successes, int trials)
    {
        if (trials == 0)
            return null;

        const double z = 1.959963984540054;
        var n = (double)trials;
        var p = successes / n;
        var zSquared = z * z;
        var denominator = 1.0 + zSquared / n;
        var center = (p + zSquared / (2.0 * n)) / denominator;
        var half = z * Math.Sqrt((p * (1.0 - p) / n) + zSquared / (4.0 * n * n)) / denominator;
        return (Math.Max(0.0, center - half), Math.Min(1.0, center + half));
    }

    private static JsonDocument Load(params string[] parts)
    {
        var root = FindRepositoryRoot();
        return JsonDocument.Parse(File.ReadAllText(Path.Combine([root, .. parts])));
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

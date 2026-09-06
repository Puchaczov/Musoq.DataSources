#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.Search.Tests.Contracts;

[TestClass]
public sealed class SearchFirstSuccessTaskSetTests
{
    private static readonly string[] BlueprintIds =
        Enumerable.Range(1, 40).Select(index => $"A{index:00}").ToArray();

    [TestMethod]
    public void DevelopmentTaskSet_ShouldMaterializeEveryBlueprintWithoutClaimingResults()
    {
        using var taskSet = Load("docs", "search", "search-first-success-task-set-v1.json");
        using var blueprints = Load("docs", "campaigns", "search", "agent-benchmark.json");
        var root = taskSet.RootElement;

        Assert.AreEqual("development_tasks_only", root.GetProperty("status").GetString());
        Assert.AreEqual(40, root.GetProperty("sourceBlueprint").GetProperty("taskCount").GetInt32());
        Assert.IsFalse(root.GetProperty("sourceBlueprint").GetProperty("holdoutAnswersIncluded").GetBoolean());
        Assert.IsFalse(root.GetProperty("measurementContract").GetProperty("resultsRecorded").GetBoolean());
        Assert.AreEqual("not_executed", root.GetProperty("measurementContract").GetProperty("resultStatus").GetString());

        var tasks = root.GetProperty("tasks").EnumerateArray().ToArray();
        Assert.AreEqual(40, tasks.Length);
        CollectionAssert.AreEqual(BlueprintIds, tasks.Select(task => task.GetProperty("id").GetString()).ToArray());

        var sourceBlueprintIds = blueprints.RootElement.GetProperty("taskBlueprints")
            .EnumerateArray()
            .Select(task => task.GetProperty("id").GetString())
            .ToArray();
        CollectionAssert.AreEqual(BlueprintIds, sourceBlueprintIds);

        var blueprintTitles = blueprints.RootElement.GetProperty("taskBlueprints")
            .EnumerateArray()
            .ToDictionary(
                task => task.GetProperty("id").GetString()!,
                task => task.GetProperty("title").GetString()!,
                StringComparer.Ordinal);

        foreach (var task in tasks)
        {
            var taskId = task.GetProperty("id").GetString()!;
            Assert.AreEqual(blueprintTitles[taskId], task.GetProperty("blueprintTitle").GetString());
            Assert.IsFalse(string.IsNullOrWhiteSpace(task.GetProperty("category").GetString()));
            Assert.IsFalse(string.IsNullOrWhiteSpace(task.GetProperty("prompt").GetString()));
            Assert.IsFalse(string.IsNullOrWhiteSpace(task.GetProperty("fixtureCase").GetString()));
            Assert.IsFalse(string.IsNullOrWhiteSpace(task.GetProperty("resultUnit").GetString()));
            Assert.IsTrue(task.TryGetProperty("firstAttempt", out var firstAttempt));
            Assert.IsFalse(string.IsNullOrWhiteSpace(firstAttempt.GetProperty("expectedOutcome").GetString()));
            Assert.IsTrue(task.TryGetProperty("success", out var success));
            Assert.IsTrue(success.TryGetProperty("query", out _) || success.TryGetProperty("action", out _));
            Assert.IsTrue(success.TryGetProperty("expectedAnswer", out var answer));
            Assert.AreNotEqual(JsonValueKind.Null, answer.ValueKind);
            Assert.IsTrue(task.TryGetProperty("results", out var results));
            Assert.AreEqual(0, results.GetArrayLength());
        }
    }

    [TestMethod]
    public void DevelopmentTaskSet_ShouldUseRegisteredSourcesAndPreserveNegativeBoundaries()
    {
        using var taskSet = Load("docs", "search", "search-first-success-task-set-v1.json");
        var root = taskSet.RootElement;
        var registered = root.GetProperty("discoveryContract").GetProperty("registeredSources")
            .EnumerateArray()
            .Select(source => source.GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        var tasks = root.GetProperty("tasks").EnumerateArray().ToArray();
        foreach (var task in tasks)
        {
            var firstQuery = task.GetProperty("firstAttempt").TryGetProperty("query", out var firstQueryElement)
                ? firstQueryElement.GetString()
                : null;
            var success = task.GetProperty("success");
            var successQuery = success.TryGetProperty("query", out var successQueryElement)
                ? successQueryElement.GetString()
                : null;

            foreach (var source in ExtractSearchSources(firstQuery).Concat(ExtractSearchSources(successQuery)))
                Assert.IsTrue(registered.Contains(source), $"Unregistered Search source '{source}' in task {task.GetProperty("id").GetString()}.");

            var firstAttemptText = firstQuery ?? task.GetProperty("firstAttempt").GetProperty("expectedOutcome").GetString();
            var successText = successQuery ?? success.GetProperty("action").GetString();
            Assert.AreNotEqual(firstAttemptText, successText, $"Task {task.GetProperty("id").GetString()} lacks a distinct repair/success path.");

            var completenessRequired = task.GetProperty("completenessRequired").GetBoolean();
            var unit = task.GetProperty("resultUnit").GetString();
            if (completenessRequired)
            {
                Assert.IsTrue(
                    unit is "count" or "path" or "matching_file" or "terminal_outcome" or "evidence_relation" or "query_result" or "parsed_record" or "byte_occurrence",
                    $"Task {task.GetProperty("id").GetString()} needs a terminal/completeness-aware result unit.");
            }
        }
    }

    [TestMethod]
    public void DevelopmentTaskSet_ShouldExposeTheMetricFieldsNeededForFirstSuccessScoring()
    {
        using var taskSet = Load("docs", "search", "search-first-success-task-set-v1.json");
        var fields = taskSet.RootElement.GetProperty("measurementContract").GetProperty("fields")
            .EnumerateArray()
            .Select(field => field.GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var required in new[]
        {
            "firstValidAttempt", "repairAttempts", "toolCalls", "inputTokens", "outputTokens",
            "completenessAware", "answerCorrect", "evidenceValid", "terminalOutcome"
        })
            Assert.IsTrue(fields.Contains(required), $"Metric '{required}' is missing.");

        var trace = new[]
        {
            new Attempt(Valid: false, Complete: false, ToolCalls: 1, InputTokens: 100, OutputTokens: 40),
            new Attempt(Valid: true, Complete: true, ToolCalls: 2, InputTokens: 120, OutputTokens: 60)
        };

        Assert.AreEqual(2, FirstValidAttempt(trace));
        Assert.AreEqual(1, RepairAttempts(trace));
        Assert.AreEqual(3, trace.Sum(attempt => attempt.ToolCalls));
        Assert.AreEqual(220, trace.Sum(attempt => attempt.InputTokens));
        Assert.AreEqual(100, trace.Sum(attempt => attempt.OutputTokens));
        Assert.IsTrue(CompletenessAware("ScopeExhausted", complete: true, countsExact: true));
        Assert.IsFalse(CompletenessAware("Failed", complete: false, countsExact: false));
        Assert.IsFalse(CompletenessAware("QuerySatisfied", complete: true, countsExact: false));
    }

    private static IEnumerable<string> ExtractSearchSources(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            yield break;

        const string prefix = "search.";
        var remaining = query;
        while (true)
        {
            var index = remaining.IndexOf(prefix, StringComparison.Ordinal);
            if (index < 0)
                yield break;

            var start = index + prefix.Length;
            var end = start;
            while (end < remaining.Length && char.IsLetter(remaining[end]))
                end++;

            if (end > start)
                yield return remaining[start..end];

            remaining = remaining[end..];
        }
    }

    private static int FirstValidAttempt(IReadOnlyList<Attempt> attempts)
    {
        for (var index = 0; index < attempts.Count; index++)
        {
            if (attempts[index].Valid && attempts[index].Complete)
                return index + 1;
        }

        return 0;
    }

    private static int RepairAttempts(IReadOnlyList<Attempt> attempts)
    {
        var first = FirstValidAttempt(attempts);
        return first == 0 ? attempts.Count : first - 1;
    }

    private static bool CompletenessAware(string terminalOutcome, bool complete, bool countsExact)
    {
        return complete && countsExact && terminalOutcome == "ScopeExhausted";
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

    private sealed record Attempt(bool Valid, bool Complete, int ToolCalls, int InputTokens, int OutputTokens);
}

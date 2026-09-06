#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Converter;
using Musoq.Evaluator;
using Musoq.Schema.Optimization;

using Musoq.DataSources.Search.Entities;

using Musoq.DataSources.Search.Components.Planning;

using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Components.Planning;

[TestClass]
public sealed class SearchPlanInspectionTests
{
    [TestMethod]
    public void Planner_ShouldExposeStableCostAndDecisionFields()
    {
        var accepted = Equal("m.Path", "fixture.txt");
        var residual = Equal("m.MatchText", "TODO");
        var result = new SearchSchema().TryPlanSource(
            "matches",
            new SourcePlanRequest
            {
                Identity = Identity(),
                RequiredColumns =
                [
                    new SourceColumnRef("m.Path"),
                    new SourceColumnRef("m.MatchText"),
                    new SourceColumnRef("m.Context")
                ],
                Predicate = new SourcePredicateLogical(
                    SourcePredicateLogicalOperator.And,
                    accepted,
                    residual),
                OrderBy =
                [
                    new OrderByExpression(
                        new SourceColumnRef("m.Context"),
                        OrderDirection.Ascending)
                ],
                Skip = 1,
                Take = 2
            });

        var inspection = GetInspection(result);
        Assert.AreEqual(1, inspection["schemaVersion"]);
        Assert.AreEqual("filesystem-text-occurrence-scan", inspection["strategy"]);
        StringAssert.Contains((string)inspection["effectiveScope"]!, "recursive=True");
        StringAssert.Contains((string)inspection["content"]!, "opens and decodes");
        StringAssert.Contains((string)inspection["decoding"]!, "BOM-aware");
        StringAssert.Contains((string)inspection["context"]!, "Context retained");
        StringAssert.Contains((string)inspection["earlyStop"]!, "TAKE is an output window");

        var acceptedOperations = GetNested(inspection, "accepted");
        CollectionAssert.AreEqual(
            new[] { "Path", "MatchText", "Context" },
            (string[])acceptedOperations["columns"]!);
        Assert.AreEqual("partial", acceptedOperations["predicate"]);
        Assert.AreEqual(0, acceptedOperations["orderBy"]);
        Assert.IsNull(acceptedOperations["skip"]);
        Assert.IsNull(acceptedOperations["take"]);

        var residualOperations = GetNested(inspection, "residual");
        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            (string[])residualOperations["columns"]!);
        Assert.AreEqual("partial", residualOperations["predicate"]);
        Assert.AreEqual(1, residualOperations["orderBy"]);
        Assert.AreEqual(1L, residualOperations["skip"]);
        Assert.AreEqual(2L, residualOperations["take"]);

        var diagnostic = result.Diagnostics.Single(item =>
            item.Optimization == SearchPlanInspection.DiagnosticOptimization);
        StringAssert.Contains(diagnostic.Message, "strategy=filesystem-text-occurrence-scan");
        StringAssert.Contains(diagnostic.Message, "effective-scope=");
        StringAssert.Contains(diagnostic.Message, "decoding=");
        StringAssert.Contains(diagnostic.Message, "accepted=");
        StringAssert.Contains(diagnostic.Message, "residual=");
        Assert.AreEqual("#search.matches", diagnostic.Target);
    }

    [TestMethod]
    public void Planner_ShouldExposeTheStrategyForEachSearchRowShape()
    {
        var expected = new Dictionary<string, (string Strategy, string Content, string Decoding)>
        {
            ["matches"] = ("filesystem-text-occurrence-scan", "opens and decodes", "BOM-aware"),
            ["lines"] = ("filesystem-text-line-scan", "opens and decodes", "BOM-aware"),
            ["files"] = ("filesystem-text-any-match-scan", "opens and decodes", "BOM-aware"),
            ["counts"] = ("filesystem-text-complete-count-scan", "opens and decodes", "BOM-aware"),
            ["paths"] = ("filesystem-metadata-path-scan", "never opens", "none; content")
        };

        foreach (var (source, values) in expected)
        {
            var result = new SearchSchema().TryPlanSource(
                source,
                new SourcePlanRequest
                {
                    Identity = new SourceIdentity("search", source, "inspection", "s")
                });
            var inspection = GetInspection(result);

            Assert.AreEqual(values.Strategy, inspection["strategy"], source);
            StringAssert.Contains((string)inspection["content"]!, values.Content, source);
            StringAssert.Contains((string)inspection["decoding"]!, values.Decoding, source);
        }
    }

    [TestMethod]
    public void Planner_ShouldLeaveManySourceDetailsResidualWithoutFalseColumnWarnings()
    {
        var result = new SearchSchema().TryPlanSource(
            "many",
            new SourcePlanRequest
            {
                Identity = new SourceIdentity("search", "many", "inspection", "m"),
                RequiredColumns = [new SourceColumnRef(nameof(SearchMatch.Path))]
            });

        var inspection = GetInspection(result);
        Assert.AreEqual("unplanned-source-residual", inspection["strategy"]);
        StringAssert.Contains((string)inspection["effectiveScope"]!, "static planner metadata is unavailable");
        Assert.IsFalse(result.ContractDiagnostics.Any(item =>
            item.Code == "UnsupportedRequiredColumn"));
        Assert.IsTrue(result.Diagnostics.Any(item =>
            item.Optimization == SearchPlanInspection.DiagnosticOptimization));
    }

    [TestMethod]
    public void CompileForInspection_ShouldNotResolveOrScanTheSourceRoot()
    {
        var missingRoot = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"musoq-search-inspection-{Guid.NewGuid():N}");
        var escapedRoot = missingRoot.Replace("\\", "\\\\", StringComparison.Ordinal);

        Assert.IsFalse(System.IO.Directory.Exists(missingRoot));
        Assert.IsFalse(System.IO.File.Exists(missingRoot));

        var inspection = InstanceCreator.CompileForInspection(
            $"select m.Path from search.matches('{escapedRoot}', 'TODO') m",
            $"SearchInspection_{Guid.NewGuid():N}",
            new SearchSchemaProvider(),
            new NullSearchLoggerResolver(),
            new CompilationOptions(
                ParallelizationMode.Full,
                usePrimitiveTypeValidation: true));

        Assert.IsFalse(inspection.Diagnostics.Any(static diagnostic => diagnostic.IsError), inspection.PlanningText);
        StringAssert.Contains(inspection.PlanningText, "strategy=filesystem-text-occurrence-scan");
        StringAssert.Contains(inspection.PlanningText, "resolved-at-execution=yes");
        Assert.IsFalse(System.IO.Directory.Exists(missingRoot));
        Assert.IsFalse(System.IO.File.Exists(missingRoot));
    }

    [TestMethod]
    public void Planner_ShouldDiagnoseMalformedAndUnsupportedRequiredColumns()
    {
        var result = new SearchSchema().TryPlanSource(
            "matches",
            new SourcePlanRequest
            {
                Identity = Identity(),
                RequiredColumns =
                [
                    new SourceColumnRef("m.Path"),
                    new SourceColumnRef("m.Unsupported"),
                    new SourceColumnRef(string.Empty)
                ]
            });

        Assert.AreEqual(1, result.AcceptedColumns.Count);
        Assert.AreEqual(nameof(SearchMatch.Path), result.AcceptedColumns[0].Name);
        Assert.IsTrue(result.ContractDiagnostics.Any(item =>
            item.Code == "UnsupportedRequiredColumn" &&
            item.ColumnName == "Unsupported"));
        Assert.IsTrue(result.ContractDiagnostics.Any(item =>
            item.Code == "MalformedRequiredColumn"));

        var inspection = GetInspection(result);
        var residual = GetNested(inspection, "residual");
        CollectionAssert.AreEqual(new[] { "Unsupported" }, (string[])residual["columns"]!);
    }

    [TestMethod]
    public void Planner_ShouldDiagnoseNullRequiredColumnMetadataWithoutScanning()
    {
        var result = new SearchSchema().TryPlanSource(
            "paths",
            new SourcePlanRequest
            {
                Identity = new SourceIdentity("search", "paths", "inspection", "p"),
                RequiredColumns = null!
            });

        Assert.AreEqual(0, result.AcceptedColumns.Count);
        Assert.IsTrue(result.ContractDiagnostics.Any(item =>
            item.Code == "MalformedRequiredColumns" &&
            item.Severity == SourceContractDiagnosticSeverity.Error));
        Assert.AreEqual("filesystem-metadata-path-scan", GetInspection(result)["strategy"]);
    }

    private static IReadOnlyDictionary<string, object?> GetInspection(SourcePlanResult result)
    {
        return (IReadOnlyDictionary<string, object?>)result.ExecutionPlan.Properties[
            SearchPlanInspection.PropertyName]!;
    }

    private static IReadOnlyDictionary<string, object?> GetNested(
        IReadOnlyDictionary<string, object?> inspection,
        string name)
    {
        return (IReadOnlyDictionary<string, object?>)inspection[name]!;
    }

    private static SourceIdentity Identity()
    {
        return new SourceIdentity("search", "matches", "inspection", "m");
    }

    private static SourcePredicateComparison Equal(string column, object? value)
    {
        return new SourcePredicateComparison(
            SourcePredicateComparisonOperator.Equal,
            new SourcePredicateColumn(new SourceColumnRef(column)),
            new SourcePredicateLiteral(value));
    }

    private sealed class NullSearchLoggerResolver : ILoggerResolver
    {
        public ILogger ResolveLogger() => NullLogger.Instance;

        public ILogger<T> ResolveLogger<T>() => NullLogger<T>.Instance;
    }
}

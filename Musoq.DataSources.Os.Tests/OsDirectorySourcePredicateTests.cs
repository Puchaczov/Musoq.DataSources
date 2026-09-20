using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Os.Directories;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Tests;

[TestClass]
public class OsDirectorySourcePredicateTests
{
    [TestMethod]
    public void NonrecursiveNamePredicate_UsesExactDirectorySearchPattern()
    {
        var root = Directory.CreateTempSubdirectory("musoq-os-directories-");
        try
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, "Match"));
            Directory.CreateDirectory(Path.Combine(root.FullName, "Other"));

            var capture = new DataSourceProgressCapture();
            var source = new DirectoriesSource(
                root.FullName,
                false,
                CreateContext(Equal("d.Name", "Match"), capture.Handler));

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("Match", rows[0].Name);
            Assert.AreEqual(1L, capture.For("directories", DataSourcePhase.RowsRead).Single().RowsProcessed);
            Assert.AreEqual(1L, capture.For("directories", DataSourcePhase.End).Single().RowsProcessed);
        }
        finally
        {
            Directory.Delete(root.FullName, true);
        }
    }

    [TestMethod]
    public void RecursiveNamePredicate_DoesNotPruneUnmatchedBranches()
    {
        var root = Directory.CreateTempSubdirectory("musoq-os-directories-");
        try
        {
            var parent = Directory.CreateDirectory(Path.Combine(root.FullName, "Parent"));
            Directory.CreateDirectory(Path.Combine(parent.FullName, "Match"));
            Directory.CreateDirectory(Path.Combine(root.FullName, "Sibling"));

            var capture = new DataSourceProgressCapture();
            var source = new DirectoriesSource(
                root.FullName,
                true,
                CreateContext(Equal("d.Name", "Match"), capture.Handler));

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("Match", rows[0].Name);
            Assert.AreEqual(3L, capture.For("directories", DataSourcePhase.RowsRead).Single().RowsProcessed);
            Assert.AreEqual(1L, capture.For("directories", DataSourcePhase.End).Single().RowsProcessed);
        }
        finally
        {
            Directory.Delete(root.FullName, true);
        }
    }

    [TestMethod]
    public void NamePredicate_UsesOrdinalEqualityAfterEnumeration()
    {
        var root = Directory.CreateTempSubdirectory("musoq-os-directories-");
        try
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, "CaseTarget"));

            var caseVariantRows = new DirectoriesSource(
                    root.FullName,
                    false,
                    CreateContext(Equal("d.Name", "casetarget")))
                .Chunks
                .SelectMany(chunk => chunk)
                .ToArray();
            var exactRows = new DirectoriesSource(
                    root.FullName,
                    false,
                    CreateContext(Equal("d.Name", "CaseTarget")))
                .Chunks
                .SelectMany(chunk => chunk)
                .ToArray();

            Assert.AreEqual(0, caseVariantRows.Length);
            Assert.AreEqual(1, exactRows.Length);
        }
        finally
        {
            Directory.Delete(root.FullName, true);
        }
    }

    private static SourceExecutionContext CreateContext(
        SourcePredicateExpression? predicate,
        DataSourceEventHandler? progressCallback = null)
    {
        var result = new OsSchema().TryPlanSource(
            "directories",
            new SourcePlanRequest
            {
                Identity = new SourceIdentity("os", "directories", "directories", "directories"),
                RequiredColumns = [],
                SourceRuntimeSettings = new Dictionary<string, string>(),
                Predicate = predicate,
                OrderBy = []
            });

        return RuntimeV2TestContexts.CreateExecutionContext(
            executionPlan: result.ExecutionPlan,
            dataSourceProgressCallback: progressCallback);
    }

    private static SourcePredicateComparison Equal(string columnName, string value)
    {
        return new SourcePredicateComparison(
            SourcePredicateComparisonOperator.Equal,
            new SourcePredicateColumn(new SourceColumnRef(columnName)),
            new SourcePredicateLiteral(value));
    }
}

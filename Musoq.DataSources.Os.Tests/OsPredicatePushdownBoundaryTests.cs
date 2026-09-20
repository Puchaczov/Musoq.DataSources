using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Os.Dlls;
using Musoq.DataSources.Os.Directories;
using Musoq.DataSources.Os.Files;
using Musoq.DataSources.Os.Tests.Utils;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Evaluator.Tables;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Tests;

[TestClass]
public class OsPredicatePushdownBoundaryTests
{
    [DataTestMethod]
    [DataRow("file", "f.Name", "File1.txt", true, false, false)]
    [DataRow("file", "f.FileName", "File1.txt", true, false, false)]
    [DataRow("file", "f.Extension", ".txt", true, false, false)]
    [DataRow("files", "f.Name", "File1.txt", true, true, false)]
    [DataRow("files", "f.FileName", "File1.txt", true, true, false)]
    [DataRow("files", "f.Extension", ".txt", true, true, false)]
    [DataRow("dlls", "d.FileInfo.Name", "File1.dll", true, false, false)]
    [DataRow("dlls", "d.FileInfo.Extension", ".dll", true, false, false)]
    [DataRow("metadata", "m.FullName", "File1.txt", false, false, false)]
    [DataRow("directories", "d.Name", "Directory1", true, false, true)]
    public void Plan_MatchesTheFinalSourceCapabilityMatrix(
        string sourceName,
        string columnName,
        string value,
        bool accepted,
        bool emitsFileFilters,
        bool emitsDirectoryFilters)
    {
        var result = PredicatePushdownTestHelpers.Plan(sourceName, PredicatePushdownTestHelpers.Equal(columnName, value));

        Assert.AreEqual(accepted, result.AcceptedPredicate is not null);
        Assert.AreEqual(!accepted, result.ResidualPredicate is not null);
        Assert.AreEqual(
            emitsFileFilters,
            result.ExecutionPlan.Properties.ContainsKey(OsSourcePlanner.FileFiltersPropertyName));
        Assert.AreEqual(
            emitsDirectoryFilters,
            result.ExecutionPlan.Properties.ContainsKey(OsSourcePlanner.DirectoryFiltersPropertyName));
    }

    [TestMethod]
    public void FileSource_ReportsOneCandidateAndFiltersBeforeEmission()
    {
        var capture = new DataSourceProgressCapture();
        var plan = PredicatePushdownTestHelpers.Plan(
            "file",
            PredicatePushdownTestHelpers.Equal("f.Name", "File1.txt"));
        var source = new FileSource(
            "./Files/File1.txt",
            PredicatePushdownTestHelpers.CreateContext(plan, capture.Handler));

        var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

        Assert.AreEqual(1, rows.Length);
        Assert.AreEqual("File1.txt", rows[0].Name);
        Assert.AreEqual(1L, PredicatePushdownTestHelpers.RowsRead(capture, "file"));
        Assert.AreEqual(1L, PredicatePushdownTestHelpers.RowsEmitted(capture, "file"));
    }

    [TestMethod]
    public void FileSource_DoesNotEmitARejectedCandidate()
    {
        var capture = new DataSourceProgressCapture();
        var plan = PredicatePushdownTestHelpers.Plan(
            "file",
            PredicatePushdownTestHelpers.Equal("f.Extension", ".bin"));
        var source = new FileSource(
            "./Files/File1.txt",
            PredicatePushdownTestHelpers.CreateContext(plan, capture.Handler));

        var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

        Assert.AreEqual(0, rows.Length);
        Assert.AreEqual(1L, PredicatePushdownTestHelpers.RowsRead(capture, "file"));
        Assert.AreEqual(0L, PredicatePushdownTestHelpers.RowsEmitted(capture, "file"));
    }

    [TestMethod]
    public void Files_AcceptedPredicateReducesCandidatesAndMaterialization()
    {
        var root = Directory.CreateTempSubdirectory("musoq-os-pushdown-");
        try
        {
            File.WriteAllText(Path.Combine(root.FullName, "Match1.txt"), "match");
            File.WriteAllText(Path.Combine(root.FullName, "Match2.txt"), "match");
            File.WriteAllText(Path.Combine(root.FullName, "Ignored.bin"), "ignored");

            var capture = new DataSourceProgressCapture();
            var source = new PredicateCountingFilesSource(
                root.FullName,
                false,
                PredicatePushdownTestHelpers.CreateContext(
                    PredicatePushdownTestHelpers.Plan(
                        "files",
                        PredicatePushdownTestHelpers.Equal("f.Extension", ".txt")),
                    capture.Handler));

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual(2L, source.FilesEnumerated);
            Assert.AreEqual(2L, PredicatePushdownTestHelpers.RowsRead(capture, "files"));
            Assert.AreEqual(2L, source.EntitiesMaterialized);
            Assert.AreEqual(2L, PredicatePushdownTestHelpers.RowsEmitted(capture, "files"));
        }
        finally
        {
            Directory.Delete(root.FullName, true);
        }
    }

    [TestMethod]
    public void Files_ResidualWildcardKeepsFilesystemWorkForEvaluator()
    {
        var root = Directory.CreateTempSubdirectory("musoq-os-pushdown-");
        try
        {
            File.WriteAllText(Path.Combine(root.FullName, "Match.txt"), "match");
            File.WriteAllText(Path.Combine(root.FullName, "Other.txt"), "other");
            File.WriteAllText(Path.Combine(root.FullName, "Ignored.bin"), "ignored");

            var capture = new DataSourceProgressCapture();
            var plan = PredicatePushdownTestHelpers.Plan(
                "files",
                PredicatePushdownTestHelpers.Equal("f.Name", "*.txt"));
            var source = new PredicateCountingFilesSource(
                root.FullName,
                false,
                PredicatePushdownTestHelpers.CreateContext(plan, capture.Handler));

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();
            var query = RunQuery(
                $"select f.Name from os.files('{PredicatePushdownTestHelpers.QueryPath(root.FullName)}', false) f " +
                "where f.Name = '*.txt'");

            Assert.IsNull(plan.AcceptedPredicate);
            Assert.IsNotNull(plan.ResidualPredicate);
            Assert.AreEqual(3, rows.Length);
            Assert.AreEqual(3L, source.FilesEnumerated);
            Assert.AreEqual(3L, PredicatePushdownTestHelpers.RowsRead(capture, "files"));
            Assert.AreEqual(3L, source.EntitiesMaterialized);
            Assert.AreEqual(3L, PredicatePushdownTestHelpers.RowsEmitted(capture, "files"));
            Assert.AreEqual(0, query.Count);
        }
        finally
        {
            Directory.Delete(root.FullName, true);
        }
    }

    [TestMethod]
    public void Files_ContradictoryAcceptedAnd_IsVerifiedAfterSearchPattern()
    {
        var root = Directory.CreateTempSubdirectory("musoq-os-pushdown-");
        try
        {
            File.WriteAllText(Path.Combine(root.FullName, "Match.txt"), "match");
            File.WriteAllText(Path.Combine(root.FullName, "Other.txt"), "other");

            var predicate = new SourcePredicateLogical(
                SourcePredicateLogicalOperator.And,
                PredicatePushdownTestHelpers.Equal("f.Name", "Match.txt"),
                PredicatePushdownTestHelpers.Equal("f.Extension", ".bin"));
            var capture = new DataSourceProgressCapture();
            var source = new PredicateCountingFilesSource(
                root.FullName,
                false,
                PredicatePushdownTestHelpers.CreateContext(
                    PredicatePushdownTestHelpers.Plan("files", predicate),
                    capture.Handler));

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(0, rows.Length);
            Assert.AreEqual(1L, source.FilesEnumerated);
            Assert.AreEqual(1L, PredicatePushdownTestHelpers.RowsRead(capture, "files"));
            Assert.AreEqual(0L, source.EntitiesMaterialized);
            Assert.AreEqual(0L, PredicatePushdownTestHelpers.RowsEmitted(capture, "files"));
        }
        finally
        {
            Directory.Delete(root.FullName, true);
        }
    }

    [TestMethod]
    public void Files_MixedAcceptedAndResidualPredicatesKeepResidualEvaluationInEvaluator()
    {
        var root = Directory.CreateTempSubdirectory("musoq-os-pushdown-");
        try
        {
            File.WriteAllText(Path.Combine(root.FullName, "Match.txt"), "match");
            File.WriteAllText(Path.Combine(root.FullName, "Other.txt"), "other");

            var predicate = new SourcePredicateLogical(
                SourcePredicateLogicalOperator.And,
                PredicatePushdownTestHelpers.Equal("f.Extension", ".txt"),
                PredicatePushdownTestHelpers.Compare(
                    SourcePredicateComparisonOperator.GreaterThan,
                    "f.Length",
                    1000));
            var plan = PredicatePushdownTestHelpers.Plan("files", predicate);
            var capture = new DataSourceProgressCapture();
            var source = new PredicateCountingFilesSource(
                root.FullName,
                false,
                PredicatePushdownTestHelpers.CreateContext(plan, capture.Handler));

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();
            var query = RunQuery(
                $"select f.Name from os.files('{PredicatePushdownTestHelpers.QueryPath(root.FullName)}', false) f " +
                "where f.Extension = '.txt' and f.Length > 1000");

            Assert.IsNotNull(plan.AcceptedPredicate);
            Assert.IsNotNull(plan.ResidualPredicate);
            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual(2L, source.FilesEnumerated);
            Assert.AreEqual(2L, source.EntitiesMaterialized);
            Assert.AreEqual(0, query.Count);
        }
        finally
        {
            Directory.Delete(root.FullName, true);
        }
    }

    [TestMethod]
    public void Dlls_KeepDllSearchPatternAndFilterBeforeEntityCreation()
    {
        var root = Directory.CreateTempSubdirectory("musoq-os-pushdown-");
        try
        {
            File.WriteAllText(Path.Combine(root.FullName, "Accepted.dll"), "not an assembly");
            File.WriteAllText(Path.Combine(root.FullName, "Rejected.dll"), "not an assembly");
            File.WriteAllText(Path.Combine(root.FullName, "Ignored.txt"), "ignored");

            var capture = new DataSourceProgressCapture();
            var plan = PredicatePushdownTestHelpers.Plan(
                "dlls",
                PredicatePushdownTestHelpers.Equal("d.FileInfo.Name", "Accepted.dll"));
            var source = new PredicateCountingDllSource(
                root.FullName,
                false,
                PredicatePushdownTestHelpers.CreateContext(plan, capture.Handler));

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.IsFalse(plan.ExecutionPlan.Properties.ContainsKey(OsSourcePlanner.FileFiltersPropertyName));
            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("Accepted.dll", rows[0].FileInfo.Name);
            Assert.AreEqual(2L, source.FilesEnumerated);
            Assert.AreEqual(2L, PredicatePushdownTestHelpers.RowsRead(capture, "files"));
            Assert.AreEqual(1L, source.EntitiesMaterialized);
            Assert.AreEqual(1L, PredicatePushdownTestHelpers.RowsEmitted(capture, "files"));
        }
        finally
        {
            Directory.Delete(root.FullName, true);
        }
    }

    [TestMethod]
    public void Metadata_KeepsFilePredicatesResidualAndPreservesExtraction()
    {
        var plan = PredicatePushdownTestHelpers.Plan(
            "metadata",
            PredicatePushdownTestHelpers.Equal("m.FullName", "IMG_2426.jpg"));
        var capture = new DataSourceProgressCapture();
        var source = new PredicateCountingMetadataSource(
            "./Images",
            PredicatePushdownTestHelpers.CreateContext(plan, capture.Handler));

        var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

        Assert.IsNull(plan.AcceptedPredicate);
        Assert.IsNotNull(plan.ResidualPredicate);
        Assert.IsFalse(plan.ExecutionPlan.Properties.ContainsKey(OsSourcePlanner.FileFiltersPropertyName));
        Assert.IsTrue(rows.Length > 0);
        Assert.AreEqual(2L, source.FilesEnumerated);
        Assert.AreEqual(2L, PredicatePushdownTestHelpers.RowsRead(capture, "files"));
        Assert.AreEqual(2L, source.EntitiesMaterialized);
        Assert.AreEqual(rows.Length, PredicatePushdownTestHelpers.RowsEmitted(capture, "files"));
    }

    [TestMethod]
    public void Directories_RecursiveAcceptedNameReadsEveryBranchButEmitsMatches()
    {
        var root = Directory.CreateTempSubdirectory("musoq-os-pushdown-");
        try
        {
            var parent = Directory.CreateDirectory(Path.Combine(root.FullName, "Parent"));
            Directory.CreateDirectory(Path.Combine(parent.FullName, "Match"));
            Directory.CreateDirectory(Path.Combine(root.FullName, "Sibling"));

            var capture = new DataSourceProgressCapture();
            var plan = PredicatePushdownTestHelpers.Plan(
                "directories",
                PredicatePushdownTestHelpers.Equal("d.Name", "Match"));
            var source = new DirectoriesSource(
                root.FullName,
                true,
                PredicatePushdownTestHelpers.CreateContext(plan, capture.Handler));

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("Match", rows[0].Name);
            Assert.AreEqual(3L, PredicatePushdownTestHelpers.RowsRead(capture, "directories"));
            Assert.AreEqual(1L, PredicatePushdownTestHelpers.RowsEmitted(capture, "directories"));
        }
        finally
        {
            Directory.Delete(root.FullName, true);
        }
    }

    private static Table RunQuery(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
                query,
                Guid.NewGuid().ToString(),
                new OsSchemaProvider(),
                EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables())
            .Run();
    }
}

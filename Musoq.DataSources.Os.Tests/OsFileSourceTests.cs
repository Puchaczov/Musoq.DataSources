using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Os.Dlls;
using Musoq.DataSources.Os.Files;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Tests;

[TestClass]
public class OsFileSourceTests
{
    static OsFileSourceTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void File_ShouldReturnSingleMetadataRow()
    {
        var table = CreateAndRunVirtualMachine(
            "select f.Name, f.Extension, f.Length from os.file('./Files/File1.txt') f").Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("File1.txt", table[0][0]);
        Assert.AreEqual(".txt", table[0][1]);
        Assert.AreEqual(new FileInfo("./Files/File1.txt").Length, table[0][2]);
    }

    [TestMethod]
    public void File_ShouldUseExistingTextContentHelper()
    {
        var table = CreateAndRunVirtualMachine(
            "select f.GetFileContent() from os.file('./Files/File1.txt') f").Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("Example file 1.", table[0][0]);
    }

    [TestMethod]
    public void File_ShouldUseExistingByteContentHelpers()
    {
        var table = CreateAndRunVirtualMachine(
            "select ToHex(f.Head(2), '|'), f.Base64File() from os.file('./Files/File1.txt') f").Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("EF|BB", table[0][0]);
        Assert.AreEqual("77u/RXhhbXBsZSBmaWxlIDEu", table[0][1]);
    }

    [TestMethod]
    public void File_ShouldApplyAcceptedNamePredicate()
    {
        var table = CreateAndRunVirtualMachine(
            "select f.Name from os.file('./Files/File1.txt') f where f.Name = 'File1.txt'").Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("File1.txt", table[0][0]);
    }

    [TestMethod]
    public void File_ShouldReturnNoRowsWhenAcceptedNamePredicateDoesNotMatch()
    {
        var table = CreateAndRunVirtualMachine(
            "select f.Name from os.file('./Files/File1.txt') f where f.Name = 'Other.txt'").Run();

        Assert.AreEqual(0, table.Count);
    }

    [TestMethod]
    public void File_ShouldApplyAcceptedExtensionPredicate()
    {
        var matching = CreateAndRunVirtualMachine(
            "select f.Name from os.file('./Files/File1.txt') f where f.Extension = '.txt'").Run();
        var nonMatching = CreateAndRunVirtualMachine(
            "select f.Name from os.file('./Files/File1.txt') f where f.Extension = '.bin'").Run();

        Assert.AreEqual(1, matching.Count);
        Assert.AreEqual("File1.txt", matching[0][0]);
        Assert.AreEqual(0, nonMatching.Count);
    }

    [TestMethod]
    public void File_ShouldApplyAcceptedFileNamePredicate()
    {
        var table = CreateAndRunVirtualMachine(
            "select f.Name from os.file('./Files/File1.txt') f where f.FileName = 'File1.txt'").Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("File1.txt", table[0][0]);
    }

    [TestMethod]
    public void Files_ShouldFilterBeforeMaterializingEntities()
    {
        var directory = Directory.CreateTempSubdirectory("musoq-os-files-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "Match1.txt"), "match");
            File.WriteAllText(Path.Combine(directory.FullName, "Match2.txt"), "match");
            File.WriteAllText(Path.Combine(directory.FullName, "Ignored.bin"), "ignored");

            var capture = new DataSourceProgressCapture();
            var source = new CountingFilesSource(
                directory.FullName,
                false,
                CreateContext(
                    "files",
                    Equal("f.Extension", ".txt"),
                    capture.Handler));

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual(2, source.EntitiesMaterialized);
            Assert.AreEqual(2L, capture.For("files", DataSourcePhase.RowsRead).Single().RowsProcessed);
            Assert.IsTrue(rows.All(row => row.Extension == ".txt"));
        }
        finally
        {
            Directory.Delete(directory.FullName, true);
        }
    }

    [TestMethod]
    public void Files_ShouldUseExactNameFilterForFilesystemCandidates()
    {
        var directory = Directory.CreateTempSubdirectory("musoq-os-files-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "Match.txt"), "match");
            File.WriteAllText(Path.Combine(directory.FullName, "Other.txt"), "other");

            var capture = new DataSourceProgressCapture();
            var source = new CountingFilesSource(
                directory.FullName,
                false,
                CreateContext(
                    "files",
                    Equal("f.Name", "Match.txt"),
                    capture.Handler));

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("Match.txt", rows[0].Name);
            Assert.AreEqual(1, source.EntitiesMaterialized);
            Assert.AreEqual(1L, capture.For("files", DataSourcePhase.RowsRead).Single().RowsProcessed);
        }
        finally
        {
            Directory.Delete(directory.FullName, true);
        }
    }

    [TestMethod]
    public void Files_ShouldVerifyCombinedAcceptedPredicatesAfterSearchPattern()
    {
        var directory = Directory.CreateTempSubdirectory("musoq-os-files-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "Match.txt"), "match");
            File.WriteAllText(Path.Combine(directory.FullName, "Other.txt"), "other");

            var source = new CountingFilesSource(
                directory.FullName,
                false,
                CreateContext(
                    "files",
                    new SourcePredicateLogical(
                        SourcePredicateLogicalOperator.And,
                        Equal("f.Name", "Match.txt"),
                        Equal("f.Extension", ".txt"))));

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("Match.txt", rows[0].Name);
            Assert.AreEqual(1, source.EntitiesMaterialized);
        }
        finally
        {
            Directory.Delete(directory.FullName, true);
        }
    }

    [TestMethod]
    public void Files_ShouldLeaveWildcardPredicateForResidualEvaluation()
    {
        var directory = Directory.CreateTempSubdirectory("musoq-os-files-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "Match.txt"), "match");

            var path = directory.FullName.Replace('\\', '/');
            var table = CreateAndRunVirtualMachine(
                $"select f.Name from os.files('{path}', false) f where f.Name = '*.txt'").Run();

            Assert.AreEqual(0, table.Count);
        }
        finally
        {
            Directory.Delete(directory.FullName, true);
        }
    }

    [TestMethod]
    public void Dlls_ShouldRejectFileInfoCandidatesBeforeEntityCreation()
    {
        var directory = Directory.CreateTempSubdirectory("musoq-os-dlls-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "Accepted.dll"), "not an assembly");
            File.WriteAllText(Path.Combine(directory.FullName, "Rejected.dll"), "not an assembly");

            var capture = new DataSourceProgressCapture();
            var source = new CountingDllSource(
                directory.FullName,
                false,
                CreateContext(
                    "dlls",
                    Equal("d.FileInfo.Name", "Accepted.dll"),
                    capture.Handler));

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("Accepted.dll", rows[0].FileInfo.Name);
            Assert.AreEqual(1, source.EntitiesMaterialized);
            Assert.AreEqual(2L, capture.For("files", DataSourcePhase.RowsRead).Single().RowsProcessed);
        }
        finally
        {
            Directory.Delete(directory.FullName, true);
        }
    }

    [TestMethod]
    public void File_ShouldReturnNoRowsForMissingPath()
    {
        var table = CreateAndRunVirtualMachine(
            "select f.Name from os.file('./Files/MissingFileForSingularSource.txt') f").Run();

        Assert.AreEqual(0, table.Count);
    }

    [TestMethod]
    public void File_ShouldReturnNoRowsForDirectoryPath()
    {
        var table = CreateAndRunVirtualMachine(
            "select f.Name from os.file('./Files') f").Run();

        Assert.AreEqual(0, table.Count);
    }

    private static CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            new OsSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static SourceExecutionContext CreateContext(
        string sourceName,
        SourcePredicateExpression predicate,
        DataSourceEventHandler? progressCallback = null)
    {
        var result = new OsSchema().TryPlanSource(
            sourceName,
            new SourcePlanRequest
            {
                Identity = new SourceIdentity("os", sourceName, sourceName, sourceName),
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

    private sealed class CountingFilesSource(
        string path,
        bool useSubDirectories,
        SourceExecutionContext executionContext)
        : FilesSource(path, useSubDirectories, executionContext)
    {
        public int EntitiesMaterialized { get; private set; }

        protected override FileEntity CreateBasedOnFile(FileInfo file, string rootDirectory)
        {
            EntitiesMaterialized++;
            return base.CreateBasedOnFile(file, rootDirectory);
        }
    }

    private sealed class CountingDllSource(
        string path,
        bool useSubDirectories,
        SourceExecutionContext executionContext)
        : DllSource(path, useSubDirectories, executionContext)
    {
        public int EntitiesMaterialized { get; private set; }

        protected override DllInfo CreateBasedOnFile(FileInfo file, string rootDirectory)
        {
            EntitiesMaterialized++;
            return new DllInfo { FileInfo = file };
        }
    }
}

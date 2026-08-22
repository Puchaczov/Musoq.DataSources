using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Os.Compare.Directories;
using Musoq.DataSources.Os.Dlls;
using Musoq.DataSources.Os.Files;
using Musoq.DataSources.Os.Metadata;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

namespace Musoq.DataSources.Os.Tests;

[TestClass]
public class OsSchemaDescribeTests
{
    static OsSchemaDescribeTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void DescSchema_ShouldListAllAvailableMethods()
    {
        var query = "desc os";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(4, table.Columns.Count(), "Should have 4 columns: Name and up to 3 parameters");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual("Param 1", table.Columns.ElementAt(2).ColumnName);
        Assert.AreEqual("Param 2", table.Columns.ElementAt(3).ColumnName);

        Assert.AreEqual(20, table.Count, "Should have 20 rows (17 single-overload methods + 3 metadata overloads)");

        var methodNames = table.Select(row => (string)row[0]).ToList();

        Assert.AreEqual(1, methodNames.Count(m => m == "file"), "Should contain 'file' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "files"), "Should contain 'files' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "directories"), "Should contain 'directories' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "zip"), "Should contain 'zip' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "processes"), "Should contain 'processes' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "dlls"), "Should contain 'dlls' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "dirscompare"), "Should contain 'dirscompare' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "cultures"), "Should contain 'cultures' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "currentculture"), "Should contain 'currentculture' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "encodings"), "Should contain 'encodings' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "timezones"), "Should contain 'timezones' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "runtime"), "Should contain 'runtime' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "drives"), "Should contain 'drives' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "specialfolders"), "Should contain 'specialfolders' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "fileattributes"), "Should contain 'fileattributes' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "environmentvariables"), "Should contain 'environmentvariables' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "pathinfo"), "Should contain 'pathinfo' method once");
        Assert.AreEqual(3, methodNames.Count(m => m == "metadata"),
            "Should contain 'metadata' method 3 times (3 overloads)");

        var fileRow = table.First(row => (string)row[0] == "file");
        Assert.AreEqual("path: System.String", (string)fileRow[1]);
        Assert.IsNull(fileRow[2], "Second parameter should be null for file method");
        Assert.IsNull(fileRow[3], "Third parameter should be null for file method");

        var filesRow = table.First(row => (string)row[0] == "files");
        Assert.AreEqual("directory: System.String", (string)filesRow[1]);
        Assert.AreEqual("useSubdirectories: System.Boolean", (string)filesRow[2]);
        Assert.IsNull(filesRow[3], "Third parameter should be null for files method");

        var processesRow = table.First(row => (string)row[0] == "processes");
        Assert.AreEqual("processes", (string)processesRow[0]);
        Assert.IsNull(processesRow[1], "processes() should have no parameters");
        Assert.IsNull(processesRow[2]);
        Assert.IsNull(processesRow[3]);

        var dirsCompareRow = table.First(row => (string)row[0] == "dirscompare");
        Assert.AreEqual("sourceDirectory: System.String", (string)dirsCompareRow[1]);
        Assert.AreEqual("destinationDirectory: System.String", (string)dirsCompareRow[2]);
        Assert.IsNull(dirsCompareRow[3]);

        var culturesRow = table.First(row => (string)row[0] == "cultures");
        Assert.IsNull(culturesRow[1]);
        Assert.IsNull(culturesRow[2]);
        Assert.IsNull(culturesRow[3]);

        var pathInfoRow = table.First(row => (string)row[0] == "pathinfo");
        Assert.AreEqual("path: System.String", (string)pathInfoRow[1]);
        Assert.IsNull(pathInfoRow[2]);
        Assert.IsNull(pathInfoRow[3]);
    }

    [TestMethod]
    public void DescFiles_ShouldReturnMethodSignature()
    {
        var query = "desc os.files";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count(), "Should have 3 columns");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual("Param 1", table.Columns.ElementAt(2).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("files", (string)row[0]);
        Assert.AreEqual("directory: System.String", (string)row[1]);
        Assert.AreEqual("useSubdirectories: System.Boolean", (string)row[2]);
    }

    [TestMethod]
    public void DescFile_ShouldReturnMethodSignature()
    {
        var query = "desc os.file";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count(), "Should have 2 columns");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("file", (string)row[0]);
        Assert.AreEqual("path: System.String", (string)row[1]);
    }

    [TestMethod]
    public void DescDirectories_ShouldReturnMethodSignature()
    {
        var query = "desc os.directories";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count());
        Assert.AreEqual(1, table.Count);

        var row = table.First();
        Assert.AreEqual("directories", (string)row[0]);
        Assert.AreEqual("directory: System.String", (string)row[1]);
        Assert.AreEqual("useSubdirectories: System.Boolean", (string)row[2],
            "Parameter name should match actual constructor parameter");
    }

    [TestMethod]
    public void DescZip_ShouldReturnMethodSignature()
    {
        var query = "desc os.zip";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count(), "Should have 2 columns (Name, Param 0)");
        Assert.AreEqual(1, table.Count);

        var row = table.First();
        Assert.AreEqual("zip", (string)row[0]);
        Assert.AreEqual("path: System.String", (string)row[1],
            "Parameter name should match actual constructor parameter");
    }

    [TestMethod]
    public void DescProcesses_ShouldReturnMethodSignatureWithNoParameters()
    {
        var query = "desc os.processes";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count(), "Should have only 1 column (Name)");
        Assert.AreEqual(1, table.Count);

        var row = table.First();
        Assert.AreEqual("processes", (string)row[0]);
    }

    [TestMethod]
    public void DescDlls_ShouldReturnMethodSignature()
    {
        var query = "desc os.dlls";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count());
        Assert.AreEqual(1, table.Count);

        var row = table.First();
        Assert.AreEqual("dlls", (string)row[0]);
        Assert.AreEqual("path: System.String", (string)row[1]);
        Assert.AreEqual("useSubdirectories: System.Boolean", (string)row[2]);
    }

    [TestMethod]
    public void DescDirsCompare_ShouldReturnMethodSignature()
    {
        var query = "desc os.dirscompare";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count());
        Assert.AreEqual(1, table.Count);

        var row = table.First();
        Assert.AreEqual("dirscompare", (string)row[0]);
        Assert.AreEqual("sourceDirectory: System.String", (string)row[1],
            "First parameter name should match actual constructor parameter");
        Assert.AreEqual("destinationDirectory: System.String", (string)row[2],
            "Second parameter name should match actual constructor parameter");
    }

    [TestMethod]
    public void DescMetadata_ShouldReturnAllOverloads()
    {
        var query = "desc os.metadata";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(4, table.Columns.Count(), "Should have 4 columns (Name, Param 0, Param 1, Param 2)");
        Assert.AreEqual(3, table.Count, "Should have 3 rows for 3 overloads");

        var methodNames = table.Select(row => (string)row[0]).ToList();
        Assert.IsTrue(methodNames.All(name => name == "metadata"), "All rows should be for metadata method");

        var overload1 = table.ElementAt(0);
        Assert.AreEqual("metadata", (string)overload1[0]);
        Assert.AreEqual("directoryOrFile: System.String", (string)overload1[1]);
        Assert.IsNull(overload1[2]);
        Assert.IsNull(overload1[3]);

        var overload2 = table.ElementAt(1);
        Assert.AreEqual("metadata", (string)overload2[0]);
        Assert.AreEqual("pathDirectoryOrFile: System.String", (string)overload2[1]);
        Assert.AreEqual("throwOnMetadataReadError: System.Boolean", (string)overload2[2]);
        Assert.IsNull(overload2[3]);

        var overload3 = table.ElementAt(2);
        Assert.AreEqual("metadata", (string)overload3[0]);
        Assert.AreEqual("directory: System.String", (string)overload3[1]);
        Assert.AreEqual("useSubDirectories: System.Boolean", (string)overload3[2]);
        Assert.AreEqual("throwOnMetadataReadError: System.Boolean", (string)overload3[3]);
    }

    [TestMethod]
    public void DescUnknownMethod_ShouldThrowException()
    {
        var query = "desc os.unknownmethod";

        try
        {
            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();
            Assert.Fail("Should have thrown an exception for unknown method");
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            Assert.IsTrue(
                message.Contains("unknownmethod", StringComparison.OrdinalIgnoreCase),
                $"Error message should mention the unknown method. Got: {message}");
            Assert.IsTrue(
                message.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Available data sources", StringComparison.OrdinalIgnoreCase),
                $"Error message should be helpful. Got: {message}");
        }
    }

    [TestMethod]
    public void DescSchema_ShouldHaveConsistentColumnTypes()
    {
        var query = "desc os";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        foreach (var column in table.Columns)
            Assert.AreEqual(typeof(string), column.ColumnType,
                $"Column '{column.ColumnName}' should be of type string");
    }

    [TestMethod]
    public void DescFiles_ShouldHaveConsistentColumnTypes()
    {
        var query = "desc os.files";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        foreach (var column in table.Columns)
            Assert.AreEqual(typeof(string), column.ColumnType,
                $"Column '{column.ColumnName}' should be of type string");
    }

    [TestMethod]
    public void DescMetadata_ShouldShowParameterTypesCorrectly()
    {
        var query = "desc os.metadata";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        var allParams = table.SelectMany(row => new[] { row[1], row[2], row[3] })
            .Where(p => p != null)
            .Select(p => (string)p)
            .ToList();

        Assert.IsTrue(allParams.Any(p => p.Contains("System.String")), "Should have System.String parameters");
        Assert.IsTrue(allParams.Any(p => p.Contains("System.Boolean")), "Should have System.Boolean parameters");
    }

    [TestMethod]
    public void DescDirsCompare_ShouldShowTwoStringParameters()
    {
        var query = "desc os.dirscompare";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        var row = table.First();

        var param1 = (string)row[1];
        var param2 = (string)row[2];

        Assert.IsTrue(param1.Contains("sourceDirectory") && param1.Contains("System.String"),
            $"First parameter should be sourceDirectory: System.String, got: {param1}");
        Assert.IsTrue(param2.Contains("destinationDirectory") && param2.Contains("System.String"),
            $"Second parameter should be destinationDirectory: System.String, got: {param2}");
    }

    [TestMethod]
    public void DescSchema_ShouldIncludeAllDataSourceTypes()
    {
        var query = "desc os";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        var methodNames = table.Select(row => (string)row[0]).Distinct().ToList();

        Assert.AreEqual(18, methodNames.Count, "Should have 18 distinct method names");
        Assert.IsTrue(methodNames.Contains("file"));
        Assert.IsTrue(methodNames.Contains("files"));
        Assert.IsTrue(methodNames.Contains("directories"));
        Assert.IsTrue(methodNames.Contains("zip"));
        Assert.IsTrue(methodNames.Contains("processes"));
        Assert.IsTrue(methodNames.Contains("dlls"));
        Assert.IsTrue(methodNames.Contains("dirscompare"));
        Assert.IsTrue(methodNames.Contains("metadata"));
        Assert.IsTrue(methodNames.Contains("cultures"));
        Assert.IsTrue(methodNames.Contains("currentculture"));
        Assert.IsTrue(methodNames.Contains("encodings"));
        Assert.IsTrue(methodNames.Contains("timezones"));
        Assert.IsTrue(methodNames.Contains("runtime"));
        Assert.IsTrue(methodNames.Contains("drives"));
        Assert.IsTrue(methodNames.Contains("specialfolders"));
        Assert.IsTrue(methodNames.Contains("fileattributes"));
        Assert.IsTrue(methodNames.Contains("environmentvariables"));
        Assert.IsTrue(methodNames.Contains("pathinfo"));
    }

    [TestMethod]
    public void DescFilesWithArgs_ShouldReturnTableSchema()
    {
        var query = "desc os.files('./Files', false)";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count());

        Assert.IsTrue(table.Count > 0, "Should have rows describing the table columns");

        var columnNames = table.Select(row => (string)row[0]).ToList();
        var expectedColumns = new[]
        {
            nameof(FileEntity.Name),
            nameof(FileEntity.FileName),
            nameof(FileEntity.CreationTime),
            nameof(FileEntity.CreationTimeUtc),
            nameof(FileEntity.LastAccessTime),
            nameof(FileEntity.LastAccessTimeUtc),
            nameof(FileEntity.LastWriteTime),
            nameof(FileEntity.LastWriteTimeUtc),
            nameof(FileEntity.Extension),
            nameof(FileEntity.FullPath),
            nameof(FileEntity.DirectoryName),
            nameof(FileEntity.DirectoryPath),
            nameof(FileEntity.Exists),
            nameof(FileEntity.IsReadOnly),
            nameof(FileEntity.Length)
        };

        foreach (var expectedColumn in expectedColumns)
            Assert.IsTrue(columnNames.Contains(expectedColumn),
                $"Should have '{expectedColumn}' column");
    }

    [TestMethod]
    public void DescFileWithArgs_ShouldReturnSameTableSchemaAsFiles()
    {
        var fileQuery = "desc os.file('./Files/File1.txt')";
        var filesQuery = "desc os.files('./Files', false)";

        var fileTable = CreateAndRunVirtualMachine(fileQuery).Run();
        var filesTable = CreateAndRunVirtualMachine(filesQuery).Run();

        Assert.AreEqual(filesTable.Count, fileTable.Count);
        Assert.AreEqual(filesTable.Columns.Count(), fileTable.Columns.Count());

        for (var i = 0; i < filesTable.Count; i++)
        {
            Assert.AreEqual(filesTable[i][0], fileTable[i][0]);
            Assert.AreEqual(filesTable[i][1], fileTable[i][1]);
            Assert.AreEqual(filesTable[i][2], fileTable[i][2]);
        }
    }

    [TestMethod]
    public void DescDirectoriesWithArgs_ShouldReturnTableSchema()
    {
        var query = "desc os.directories('./Directories', false)";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count());
        Assert.IsTrue(table.Count > 0);

        var columnNames = table.Select(row => (string)row[0]).ToList();
        var expectedColumns = new[]
        {
            nameof(DirectoryInfo.FullName),
            nameof(DirectoryInfo.Attributes),
            nameof(DirectoryInfo.CreationTime),
            nameof(DirectoryInfo.CreationTimeUtc),
            nameof(DirectoryInfo.LastAccessTime),
            nameof(DirectoryInfo.LastAccessTimeUtc),
            nameof(DirectoryInfo.LastWriteTime),
            nameof(DirectoryInfo.LastWriteTimeUtc),
            nameof(DirectoryInfo.Exists),
            nameof(DirectoryInfo.Extension),
            nameof(DirectoryInfo.Name),
            nameof(DirectoryInfo.Parent),
            nameof(DirectoryInfo.Root),
            "DirectoryInfo"
        };

        foreach (var expectedColumn in expectedColumns)
            Assert.IsTrue(columnNames.Contains(expectedColumn),
                $"Should have '{expectedColumn}' column");
    }

    [TestMethod]
    public void DescProcessesWithArgs_ShouldReturnTableSchema()
    {
        var query = "desc os.processes()";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count());
        Assert.IsTrue(table.Count > 0);

        var columnNames = table.Select(row => (string)row[0]).ToList();
        var expectedColumns = new[]
        {
            nameof(System.Diagnostics.Process.BasePriority),
            nameof(System.Diagnostics.Process.EnableRaisingEvents),
            nameof(System.Diagnostics.Process.ExitCode),
            nameof(System.Diagnostics.Process.ExitTime),
            nameof(System.Diagnostics.Process.Handle),
            nameof(System.Diagnostics.Process.HandleCount),
            nameof(System.Diagnostics.Process.HasExited),
            nameof(System.Diagnostics.Process.Id),
            nameof(System.Diagnostics.Process.MachineName),
            nameof(System.Diagnostics.Process.MainWindowTitle),
            "PagedMemorySize64",
            nameof(System.Diagnostics.Process.ProcessName),
            nameof(System.Diagnostics.Process.ProcessorAffinity),
            nameof(System.Diagnostics.Process.Responding),
            nameof(System.Diagnostics.Process.StartTime),
            nameof(System.Diagnostics.Process.TotalProcessorTime),
            nameof(System.Diagnostics.Process.UserProcessorTime),
            "Directory",
            "FileName"
        };

        foreach (var expectedColumn in expectedColumns)
            Assert.IsTrue(columnNames.Contains(expectedColumn),
                $"Should have '{expectedColumn}' column");
    }

    [TestMethod]
    public void DescDirsCompareWithArgs_ShouldReturnTableSchema()
    {
        var query = "desc os.dirscompare('./Directories', './Directories')";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count());
        Assert.IsTrue(table.Count > 0);

        var columnNames = table.Select(row => (string)row[0]).ToList();
        var expectedColumns = new[]
        {
            nameof(CompareDirectoriesResult.SourceFile),
            nameof(CompareDirectoriesResult.DestinationFile),
            nameof(CompareDirectoriesResult.State),
            nameof(CompareDirectoriesResult.SourceRoot),
            nameof(CompareDirectoriesResult.DestinationRoot),
            nameof(CompareDirectoriesResult.SourceFileRelative),
            nameof(CompareDirectoriesResult.DestinationFileRelative)
        };

        foreach (var expectedColumn in expectedColumns)
            Assert.IsTrue(columnNames.Contains(expectedColumn),
                $"Should have '{expectedColumn}' column");
    }

    [TestMethod]
    public void DescMetadataWithArgs_ShouldReturnTableSchema()
    {
        var query = "desc os.metadata('./Images')";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count());
        Assert.IsTrue(table.Count > 0);

        var columnNames = table.Select(row => (string)row[0]).ToList();
        var expectedColumns = new[]
        {
            nameof(MetadataEntity.FullName),
            nameof(MetadataEntity.DirectoryName),
            nameof(MetadataEntity.TagName),
            nameof(MetadataEntity.Description)
        };

        foreach (var expectedColumn in expectedColumns)
            Assert.IsTrue(columnNames.Contains(expectedColumn),
                $"Should have '{expectedColumn}' column");
    }

    [TestMethod]
    public void DescZipWithArgs_ShouldReturnTableSchema()
    {
        var query = "desc os.zip('./TestZip.zip')";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count());
        Assert.IsTrue(table.Count > 0);

        var columnNames = table.Select(row => (string)row[0]).ToList();
        var expectedColumns = new[]
        {
            nameof(ZipArchiveEntry.Name),
            nameof(ZipArchiveEntry.FullName),
            nameof(ZipArchiveEntry.CompressedLength),
            nameof(ZipArchiveEntry.LastWriteTime),
            nameof(ZipArchiveEntry.Length),
            "IsDirectory",
            "Level"
        };

        foreach (var expectedColumn in expectedColumns)
            Assert.IsTrue(columnNames.Contains(expectedColumn),
                $"Should have '{expectedColumn}' column");
    }

    [TestMethod]
    public void DescDllsWithArgs_ShouldReturnTableSchema()
    {
        var query = "desc os.dlls('./TestDll.dll')";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count());
        Assert.IsTrue(table.Count > 0);

        var columnNames = table.Select(row => (string)row[0]).ToList();
        var expectedColumns = new[]
        {
            nameof(DllInfo.FileInfo),
            nameof(DllInfo.Assembly),
            nameof(DllInfo.Version)
        };

        foreach (var expectedColumn in expectedColumns)
            Assert.IsTrue(columnNames.Contains(expectedColumn),
                $"Should have '{expectedColumn}' column");
    }

    [TestMethod]
    public void DescFilesNoArgs_VsWithArgs_ShouldReturnDifferentResults()
    {
        var queryNoArgs = "desc os.files";
        var vmNoArgs = CreateAndRunVirtualMachine(queryNoArgs);
        var tableNoArgs = vmNoArgs.Run();

        var queryWithArgs = "desc os.files('./Files', false)";
        var vmWithArgs = CreateAndRunVirtualMachine(queryWithArgs);
        var tableWithArgs = vmWithArgs.Run();

        Assert.AreNotEqual(tableNoArgs.Count, tableWithArgs.Count,
            "Method signature vs table schema should have different row counts");

        Assert.AreEqual(1, tableNoArgs.Count);
        Assert.AreEqual("files", (string)tableNoArgs.First()[0]);

        Assert.IsTrue(tableWithArgs.Count > 1);
        var columnNames = tableWithArgs.Select(row => (string)row[0]).ToList();
        Assert.IsTrue(columnNames.Contains(nameof(FileEntity.Name)));
        Assert.IsTrue(columnNames.Contains(nameof(FileEntity.FullPath)));
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            new OsSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }
}

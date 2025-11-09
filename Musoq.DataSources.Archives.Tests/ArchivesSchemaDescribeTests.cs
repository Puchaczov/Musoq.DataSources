using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using SharpCompress.Common;

namespace Musoq.DataSources.Archives.Tests;

[TestClass]
public class ArchivesSchemaDescribeTests
{
    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            new ArchivesSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    static ArchivesSchemaDescribeTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void DescSchema_ShouldListAllAvailableMethods()
    {
        var query = "desc #archives";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count(), "Should have 2 columns: Name and Param 0");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have 1 row (file method)");

        var row = table.First();
        Assert.AreEqual("file", (string)row[0]);
        Assert.AreEqual("path: System.String", (string)row[1]);
    }

    [TestMethod]
    public void DescFile_ShouldReturnMethodSignature()
    {
        var query = "desc #archives.file";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count(), "Should have 2 columns: Name and Param 0");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("file", (string)row[0]);
        Assert.AreEqual("path: System.String", (string)row[1]);
    }

    [TestMethod]
    public void DescFileWithArgs_ShouldReturnTableSchema()
    {
        var query = "desc #archives.file('./Files/Example1/archives.zip')";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count());
        Assert.IsTrue(table.Count > 0, "Should have rows describing the table columns");

        var columnNames = table.Select(row => (string)row[0]).ToList();
        var expectedColumns = new[]
        {
            nameof(EntryWrapper.CompressionType),
            nameof(EntryWrapper.ArchivedTime),
            nameof(EntryWrapper.CompressedSize),
            nameof(EntryWrapper.Crc),
            nameof(EntryWrapper.CreatedTime),
            nameof(EntryWrapper.Key),
            nameof(EntryWrapper.LinkTarget),
            nameof(EntryWrapper.IsDirectory),
            nameof(EntryWrapper.IsEncrypted),
            nameof(EntryWrapper.IsSplitAfter),
            nameof(EntryWrapper.IsSolid),
            nameof(EntryWrapper.VolumeIndexFirst),
            nameof(EntryWrapper.VolumeIndexLast),
            nameof(EntryWrapper.LastAccessedTime),
            nameof(EntryWrapper.LastModifiedTime),
            nameof(EntryWrapper.Size),
            nameof(EntryWrapper.Attrib)
        };

        foreach (var expectedColumn in expectedColumns)
        {
            Assert.IsTrue(columnNames.Contains(expectedColumn),
                $"Should have '{expectedColumn}' column");
        }
    }

    [TestMethod]
    public void DescUnknownMethod_ShouldThrowException()
    {
        var query = "desc #archives.unknownmethod";

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
        var query = "desc #archives";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        foreach (var column in table.Columns)
        {
            Assert.AreEqual(typeof(string), column.ColumnType,
                $"Column '{column.ColumnName}' should be of type string");
        }
    }

    [TestMethod]
    public void DescFileNoArgs_VsWithArgs_ShouldReturnDifferentResults()
    {
        var queryNoArgs = "desc #archives.file";
        var vmNoArgs = CreateAndRunVirtualMachine(queryNoArgs);
        var tableNoArgs = vmNoArgs.Run();

        var queryWithArgs = "desc #archives.file('./Files/Example1/archives.zip')";
        var vmWithArgs = CreateAndRunVirtualMachine(queryWithArgs);
        var tableWithArgs = vmWithArgs.Run();

        Assert.AreNotEqual(tableNoArgs.Count, tableWithArgs.Count,
            "Method signature vs table schema should have different row counts");

        Assert.AreEqual(1, tableNoArgs.Count);
        Assert.AreEqual("file", (string)tableNoArgs.First()[0]);

        Assert.IsTrue(tableWithArgs.Count > 1);
        var columnNames = tableWithArgs.Select(row => (string)row[0]).ToList();
        Assert.IsTrue(columnNames.Contains(nameof(EntryWrapper.Key)));
        Assert.IsTrue(columnNames.Contains(nameof(EntryWrapper.Size)));
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using System;
using System.IO;
using System.Linq;

namespace Musoq.DataSources.FlatFile.Tests;

[TestClass]
public class FlatFileSchemaDescribeTests
{
    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            new FlatFileSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    static FlatFileSchemaDescribeTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void DescSchema_ShouldListAllAvailableMethods()
    {
        var query = "desc #flat";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count(), "Should have 2 columns: Name and Param 0");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have 1 row (1 unique method)");

        var methodNames = table.Select(row => (string)row[0]).ToList();

        Assert.AreEqual(1, methodNames.Count(m => m == "file"), "Should contain 'file' method once");

        var fileRow = table.First(row => (string)row[0] == "file");
        Assert.AreEqual("filePath: System.String", (string)fileRow[1]);
    }

    [TestMethod]
    public void DescFile_ShouldReturnMethodSignature()
    {
        var query = "desc #flat.file";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count(), "Should have 2 columns");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("file", (string)row[0]);
        Assert.AreEqual("filePath: System.String", (string)row[1]);
    }

    [TestMethod]
    public void DescFileWithArgs_ShouldReturnTableSchema()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "line1\nline2\n");

        try
        {
            var query = $"desc #flat.file('{tempFile.Replace("\\", "\\\\")}')";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(3, table.Columns.Count());
            Assert.IsTrue(table.Count > 0, "Should have rows describing the table columns");

            var columnNames = table.Select(row => (string)row[0]).ToList();
            var expectedColumns = new[]
            {
                nameof(FlatFileEntity.Line),
                nameof(FlatFileEntity.LineNumber)
            };

            foreach (var expectedColumn in expectedColumns)
            {
                Assert.IsTrue(columnNames.Contains(expectedColumn),
                    $"Should have '{expectedColumn}' column");
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [TestMethod]
    public void DescUnknownMethod_ShouldThrowException()
    {
        var query = "desc #flat.unknownmethod";

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
        var query = "desc #flat";

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
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "line1\n");

        try
        {
            var queryNoArgs = "desc #flat.file";
            var vmNoArgs = CreateAndRunVirtualMachine(queryNoArgs);
            var tableNoArgs = vmNoArgs.Run();

            var queryWithArgs = $"desc #flat.file('{tempFile.Replace("\\", "\\\\")}')";
            var vmWithArgs = CreateAndRunVirtualMachine(queryWithArgs);
            var tableWithArgs = vmWithArgs.Run();

            Assert.AreNotEqual(tableNoArgs.Count, tableWithArgs.Count,
                "Method signature vs table schema should have different row counts");

            Assert.AreEqual(1, tableNoArgs.Count);
            Assert.AreEqual("file", (string)tableNoArgs.First()[0]);

            Assert.IsTrue(tableWithArgs.Count > 1);
            var columnNames = tableWithArgs.Select(row => (string)row[0]).ToList();
            Assert.IsTrue(columnNames.Contains(nameof(FlatFileEntity.Line)));
            Assert.IsTrue(columnNames.Contains(nameof(FlatFileEntity.LineNumber)));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Musoq.Schema;

namespace Musoq.DataSources.System.Tests;

[TestClass]
public class SystemSchemaDescribeTests
{
    static SystemSchemaDescribeTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        var mockSchemaProvider = new Mock<ISchemaProvider>();
        mockSchemaProvider.Setup(x => x.GetSchema(It.IsAny<string>()))
            .Returns(new SystemSchema());

        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            mockSchemaProvider.Object,
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    [TestMethod]
    public void DescSchema_ShouldListAllAvailableMethods()
    {
        var query = "desc #system";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count(), "Should have 3 columns: Name and up to 2 parameters");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual("Param 1", table.Columns.ElementAt(2).ColumnName);

        Assert.AreEqual(3, table.Count, "Should have 3 rows (dual + 2 range overloads)");

        var methodNames = table.Select(row => (string)row[0]).ToList();

        Assert.AreEqual(1, methodNames.Count(m => m == "dual"), "Should contain 'dual' method once");
        Assert.AreEqual(2, methodNames.Count(m => m == "range"), "Should contain 'range' method twice (2 overloads)");

        var dualRow = table.First(row => (string)row[0] == "dual");
        Assert.IsNull(dualRow[1], "Dual should have no parameters");
        Assert.IsNull(dualRow[2], "Dual should have no parameters");
    }

    [TestMethod]
    public void DescDual_ShouldReturnMethodSignatureWithNoParameters()
    {
        var query = "desc #system.dual";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count(), "Should have only 1 column (Name)");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("dual", (string)row[0]);
    }

    [TestMethod]
    public void DescRange_ShouldReturnAllOverloads()
    {
        var query = "desc #system.range";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count(), "Should have 3 columns (Name, Param 0, Param 1)");
        Assert.AreEqual(2, table.Count, "Should have 2 rows for 2 overloads");

        var methodNames = table.Select(row => (string)row[0]).ToList();
        Assert.IsTrue(methodNames.All(name => name == "range"), "All rows should be for range method");

        var overload1 = table.ElementAt(0);
        Assert.AreEqual("range", (string)overload1[0]);
        Assert.AreEqual("max: System.Int64", (string)overload1[1]);
        Assert.IsNull(overload1[2]);

        var overload2 = table.ElementAt(1);
        Assert.AreEqual("range", (string)overload2[0]);
        Assert.AreEqual("min: System.Int64", (string)overload2[1]);
        Assert.AreEqual("max: System.Int64", (string)overload2[2]);
    }

    [TestMethod]
    public void DescUnknownMethod_ShouldThrowException()
    {
        var query = "desc #system.unknownmethod";

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
        var query = "desc #system";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        foreach (var column in table.Columns)
        {
            Assert.AreEqual(typeof(string), column.ColumnType,
                $"Column '{column.ColumnName}' should be of type string");
        }
    }

    [TestMethod]
    public void DescRangeNoArgs_VsWithArgs_ShouldReturnDifferentResults()
    {
        var queryNoArgs = "desc #system.range";
        var vmNoArgs = CreateAndRunVirtualMachine(queryNoArgs);
        var tableNoArgs = vmNoArgs.Run();

        var queryWithArgs = "desc #system.range(100)";
        var vmWithArgs = CreateAndRunVirtualMachine(queryWithArgs);
        var tableWithArgs = vmWithArgs.Run();

        Assert.AreEqual(2, tableNoArgs.Count, "Should have 2 overloads");

        Assert.IsTrue(tableWithArgs.Count > 0, "Should have rows describing the table columns");
        Assert.AreNotEqual(tableNoArgs.Count, tableWithArgs.Count,
            "Method signature vs table schema should have different row counts");
    }
}

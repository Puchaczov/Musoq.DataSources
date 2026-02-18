using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.Time.Tests;

[TestClass]
public class TimeSchemaDescribeTests
{
    static TimeSchemaDescribeTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        var mockSchemaProvider = new Mock<ISchemaProvider>();
        mockSchemaProvider.Setup(x => x.GetSchema(It.IsAny<string>()))
            .Returns(new TimeSchema());

        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            mockSchemaProvider.Object,
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    [TestMethod]
    public void DescSchema_ShouldListAllAvailableMethods()
    {
        var query = "desc #time";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(4, table.Columns.Count(), "Should have 4 columns: Name and 3 parameters");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual("Param 1", table.Columns.ElementAt(2).ColumnName);
        Assert.AreEqual("Param 2", table.Columns.ElementAt(3).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have 1 row for interval method");

        var row = table.First();
        Assert.AreEqual("interval", (string)row[0]);
        Assert.AreEqual("startAt: System.DateTimeOffset", (string)row[1]);
        Assert.AreEqual("stopAt: System.DateTimeOffset", (string)row[2]);
        Assert.AreEqual("resolution: System.String", (string)row[3]);
    }

    [TestMethod]
    public void DescInterval_ShouldReturnMethodSignature()
    {
        var query = "desc #time.interval";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(4, table.Columns.Count(), "Should have 4 columns");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual("Param 1", table.Columns.ElementAt(2).ColumnName);
        Assert.AreEqual("Param 2", table.Columns.ElementAt(3).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("interval", (string)row[0]);
        Assert.AreEqual("startAt: System.DateTimeOffset", (string)row[1]);
        Assert.AreEqual("stopAt: System.DateTimeOffset", (string)row[2]);
        Assert.AreEqual("resolution: System.String", (string)row[3]);
    }

    [TestMethod]
    public void DescUnknownMethod_ShouldThrowException()
    {
        var query = "desc #time.unknownmethod";

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
        var query = "desc #time";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        foreach (var column in table.Columns)
            Assert.AreEqual(typeof(string), column.ColumnType,
                $"Column '{column.ColumnName}' should be of type string");
    }

    [TestMethod]
    public void DescIntervalNoArgs_VsWithArgs_ShouldReturnDifferentResults()
    {
        var queryNoArgs = "desc #time.interval";
        var vmNoArgs = CreateAndRunVirtualMachine(queryNoArgs);
        var tableNoArgs = vmNoArgs.Run();

        var queryWithArgs = "desc #time.interval('2024-01-01', '2024-01-02', 'hours')";
        var vmWithArgs = CreateAndRunVirtualMachine(queryWithArgs);
        var tableWithArgs = vmWithArgs.Run();

        Assert.AreEqual(1, tableNoArgs.Count);
        Assert.AreEqual("interval", (string)tableNoArgs.First()[0]);

        Assert.IsTrue(tableWithArgs.Count > 1, "Should have rows describing the table columns");
        Assert.AreNotEqual(tableNoArgs.Count, tableWithArgs.Count,
            "Method signature vs table schema should have different row counts");
    }
}
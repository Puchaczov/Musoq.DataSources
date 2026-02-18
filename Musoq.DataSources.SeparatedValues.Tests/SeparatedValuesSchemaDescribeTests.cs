using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class SeparatedValuesSchemaDescribeTests
{
    static SeparatedValuesSchemaDescribeTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        var mockSchemaProvider = new Mock<ISchemaProvider>();
        mockSchemaProvider.Setup(x => x.GetSchema(It.IsAny<string>()))
            .Returns(new SeparatedValuesSchema());

        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            mockSchemaProvider.Object,
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    [TestMethod]
    public void DescSchema_ShouldListAllAvailableMethods()
    {
        var query = "desc #separatedvalues";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(4, table.Columns.Count(), "Should have 4 columns: Name and 3 parameters");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual("Param 1", table.Columns.ElementAt(2).ColumnName);
        Assert.AreEqual("Param 2", table.Columns.ElementAt(3).ColumnName);

        Assert.AreEqual(3, table.Count, "Should have 3 rows for comma, tab, semicolon methods");

        var methodNames = table.Select(row => (string)row[0]).ToList();

        Assert.AreEqual(1, methodNames.Count(m => m == "comma"), "Should contain 'comma' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "tab"), "Should contain 'tab' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "semicolon"), "Should contain 'semicolon' method once");

        var commaRow = table.First(row => (string)row[0] == "comma");
        Assert.AreEqual("path: System.String", (string)commaRow[1]);
        Assert.AreEqual("hasHeader: System.Boolean", (string)commaRow[2]);
        Assert.AreEqual("skipLines: System.Int32", (string)commaRow[3]);
    }

    [TestMethod]
    public void DescComma_ShouldReturnMethodSignature()
    {
        var query = "desc #separatedvalues.comma";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(4, table.Columns.Count(), "Should have 4 columns");
        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("comma", (string)row[0]);
        Assert.AreEqual("path: System.String", (string)row[1]);
        Assert.AreEqual("hasHeader: System.Boolean", (string)row[2]);
        Assert.AreEqual("skipLines: System.Int32", (string)row[3]);
    }

    [TestMethod]
    public void DescTab_ShouldReturnMethodSignature()
    {
        var query = "desc #separatedvalues.tab";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(4, table.Columns.Count(), "Should have 4 columns");
        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("tab", (string)row[0]);
        Assert.AreEqual("path: System.String", (string)row[1]);
        Assert.AreEqual("hasHeader: System.Boolean", (string)row[2]);
        Assert.AreEqual("skipLines: System.Int32", (string)row[3]);
    }

    [TestMethod]
    public void DescSemicolon_ShouldReturnMethodSignature()
    {
        var query = "desc #separatedvalues.semicolon";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(4, table.Columns.Count(), "Should have 4 columns");
        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("semicolon", (string)row[0]);
        Assert.AreEqual("path: System.String", (string)row[1]);
        Assert.AreEqual("hasHeader: System.Boolean", (string)row[2]);
        Assert.AreEqual("skipLines: System.Int32", (string)row[3]);
    }

    [TestMethod]
    public void DescUnknownMethod_ShouldThrowException()
    {
        var query = "desc #separatedvalues.unknownmethod";

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
        var query = "desc #separatedvalues";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        foreach (var column in table.Columns)
            Assert.AreEqual(typeof(string), column.ColumnType,
                $"Column '{column.ColumnName}' should be of type string");
    }
}
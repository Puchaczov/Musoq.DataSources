using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Musoq.Schema;

namespace Musoq.DataSources.Ollama.Tests;

[TestClass]
public class OllamaSchemaDescribeTests
{
    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        var mockSchemaProvider = new Mock<ISchemaProvider>();
        mockSchemaProvider.Setup(f => f.GetSchema(It.IsAny<string>())).Returns(new OllamaSchema());

        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            mockSchemaProvider.Object,
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    static OllamaSchemaDescribeTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void DescSchema_ShouldListAllAvailableMethods()
    {
        var query = "desc #ollama";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count(), "Should have 3 columns: Name and up to 2 parameters");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual("Param 1", table.Columns.ElementAt(2).ColumnName);

        Assert.AreEqual(2, table.Count, "Should have 2 rows (2 llm overloads)");

        var methodNames = table.Select(row => (string)row[0]).ToList();
        Assert.AreEqual(2, methodNames.Count(m => m == "llm"), "Should contain 'llm' method 2 times (2 overloads)");

        var overload1 = table.ElementAt(0);
        Assert.AreEqual("llm", (string)overload1[0]);
        Assert.AreEqual("model: System.String", (string)overload1[1]);
        Assert.IsNull(overload1[2], "First overload should have only 1 parameter");

        var overload2 = table.ElementAt(1);
        Assert.AreEqual("llm", (string)overload2[0]);
        Assert.AreEqual("model: System.String", (string)overload2[1]);
        Assert.AreEqual("temperature: System.Decimal", (string)overload2[2]);
    }

    [TestMethod]
    public void DescLlm_ShouldReturnAllOverloads()
    {
        var query = "desc #ollama.llm";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count(), "Should have 3 columns (Name, Param 0, Param 1)");
        Assert.AreEqual(2, table.Count, "Should have 2 rows for 2 overloads");

        var methodNames = table.Select(row => (string)row[0]).ToList();
        Assert.IsTrue(methodNames.All(name => name == "llm"), "All rows should be for llm method");

        var overload1 = table.ElementAt(0);
        Assert.AreEqual("llm", (string)overload1[0]);
        Assert.AreEqual("model: System.String", (string)overload1[1]);
        Assert.IsNull(overload1[2]);

        var overload2 = table.ElementAt(1);
        Assert.AreEqual("llm", (string)overload2[0]);
        Assert.AreEqual("model: System.String", (string)overload2[1]);
        Assert.AreEqual("temperature: System.Decimal", (string)overload2[2]);
    }

    [TestMethod]
    public void DescUnknownMethod_ShouldThrowException()
    {
        var query = "desc #ollama.unknownmethod";

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
                message.Contains("Available methods", StringComparison.OrdinalIgnoreCase),
                $"Error message should be helpful. Got: {message}");
        }
    }

    [TestMethod]
    public void DescSchema_ShouldHaveConsistentColumnTypes()
    {
        var query = "desc #ollama";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        foreach (var column in table.Columns)
        {
            Assert.AreEqual(typeof(string), column.ColumnType,
                $"Column '{column.ColumnName}' should be of type string");
        }
    }

    [TestMethod]
    public void DescLlmNoArgs_VsWithArgs_ShouldReturnDifferentResults()
    {
        var queryNoArgs = "desc #ollama.llm";
        var vmNoArgs = CreateAndRunVirtualMachine(queryNoArgs);
        var tableNoArgs = vmNoArgs.Run();

        Assert.AreEqual(2, tableNoArgs.Count, "Should have 2 overloads");
        var methodNames = tableNoArgs.Select(row => (string)row[0]).ToList();
        Assert.IsTrue(methodNames.All(name => name == "llm"));
    }
}

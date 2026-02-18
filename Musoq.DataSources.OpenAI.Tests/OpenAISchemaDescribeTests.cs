using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.OpenAI.Tests;

[TestClass]
public class OpenAISchemaDescribeTests
{
    static OpenAISchemaDescribeTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        var mockSchemaProvider = new Mock<ISchemaProvider>();
        mockSchemaProvider.Setup(f => f.GetSchema(It.IsAny<string>())).Returns(new OpenAiSchema());

        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            mockSchemaProvider.Object,
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    [TestMethod]
    public void DescSchema_ShouldListAllAvailableMethods()
    {
        var query = "desc #openai";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(6, table.Columns.Count(), "Should have 6 columns: Name and up to 5 parameters");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual("Param 1", table.Columns.ElementAt(2).ColumnName);
        Assert.AreEqual("Param 2", table.Columns.ElementAt(3).ColumnName);
        Assert.AreEqual("Param 3", table.Columns.ElementAt(4).ColumnName);
        Assert.AreEqual("Param 4", table.Columns.ElementAt(5).ColumnName);

        Assert.AreEqual(5, table.Count, "Should have 5 rows (5 gpt overloads)");

        var methodNames = table.Select(row => (string)row[0]).ToList();
        Assert.AreEqual(5, methodNames.Count(m => m == "gpt"), "Should contain 'gpt' method 5 times (5 overloads)");
    }

    [TestMethod]
    public void DescGpt_ShouldReturnAllOverloads()
    {
        var query = "desc #openai.gpt";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(6, table.Columns.Count(), "Should have 6 columns (Name, Param 0-4)");
        Assert.AreEqual(5, table.Count, "Should have 5 rows for 5 overloads");

        var methodNames = table.Select(row => (string)row[0]).ToList();
        Assert.IsTrue(methodNames.All(name => name == "gpt"), "All rows should be for gpt method");

        var overload1 = table.ElementAt(0);
        Assert.AreEqual("gpt", (string)overload1[0]);
        Assert.IsNull(overload1[1], "First overload should have no parameters");

        var overload2 = table.ElementAt(1);
        Assert.AreEqual("gpt", (string)overload2[0]);
        Assert.AreEqual("model: System.String", (string)overload2[1]);
        Assert.IsNull(overload2[2]);

        var overload3 = table.ElementAt(2);
        Assert.AreEqual("gpt", (string)overload3[0]);
        Assert.AreEqual("model: System.String", (string)overload3[1]);
        Assert.AreEqual("maxTokens: System.Int32", (string)overload3[2]);
        Assert.IsNull(overload3[3]);

        var overload4 = table.ElementAt(3);
        Assert.AreEqual("gpt", (string)overload4[0]);
        Assert.AreEqual("model: System.String", (string)overload4[1]);
        Assert.AreEqual("maxTokens: System.Int32", (string)overload4[2]);
        Assert.AreEqual("temperature: System.Single", (string)overload4[3]);
        Assert.IsNull(overload4[4]);

        var overload5 = table.ElementAt(4);
        Assert.AreEqual("gpt", (string)overload5[0]);
        Assert.AreEqual("model: System.String", (string)overload5[1]);
        Assert.AreEqual("maxTokens: System.Int32", (string)overload5[2]);
        Assert.AreEqual("temperature: System.Single", (string)overload5[3]);
        Assert.AreEqual("frequencyPenalty: System.Single", (string)overload5[4]);
        Assert.AreEqual("presencePenalty: System.Single", (string)overload5[5]);
    }

    [TestMethod]
    public void DescUnknownMethod_ShouldThrowException()
    {
        var query = "desc #openai.unknownmethod";

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
        var query = "desc #openai";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        foreach (var column in table.Columns)
            Assert.AreEqual(typeof(string), column.ColumnType,
                $"Column '{column.ColumnName}' should be of type string");
    }

    [TestMethod]
    public void DescGptNoArgs_VsWithArgs_ShouldReturnDifferentResults()
    {
        var queryNoArgs = "desc #openai.gpt";
        var vmNoArgs = CreateAndRunVirtualMachine(queryNoArgs);
        var tableNoArgs = vmNoArgs.Run();

        Assert.AreEqual(5, tableNoArgs.Count, "Should have 5 overloads");
        var methodNames = tableNoArgs.Select(row => (string)row[0]).ToList();
        Assert.IsTrue(methodNames.All(name => name == "gpt"));
    }
}
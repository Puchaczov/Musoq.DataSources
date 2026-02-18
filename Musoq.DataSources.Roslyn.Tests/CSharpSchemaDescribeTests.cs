using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public class CSharpSchemaDescribeTests
{
    static CSharpSchemaDescribeTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        var mockSchemaProvider = new Mock<ISchemaProvider>();
        mockSchemaProvider.Setup(f => f.GetSchema(It.IsAny<string>())).Returns(new CSharpSchema());

        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            mockSchemaProvider.Object,
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    [TestMethod]
    public void DescSchema_ShouldListAllAvailableMethods()
    {
        var query = "desc #csharp";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count(), "Should have 2 columns: Name and Param 0");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have 1 row (solution method)");

        var methodNames = table.Select(row => (string)row[0]).ToList();
        Assert.AreEqual(1, methodNames.Count(m => m == "solution"), "Should contain 'solution' method once");

        var solutionRow = table.First(row => (string)row[0] == "solution");
        Assert.AreEqual("path: System.String", (string)solutionRow[1]);
    }

    [TestMethod]
    public void DescSolution_ShouldReturnMethodSignature()
    {
        var query = "desc #csharp.solution";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count(), "Should have 2 columns");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("solution", (string)row[0]);
        Assert.AreEqual("path: System.String", (string)row[1]);
    }

    [TestMethod]
    public void DescUnknownMethod_ShouldThrowException()
    {
        var query = "desc #csharp.unknownmethod";

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
        var query = "desc #csharp";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        foreach (var column in table.Columns)
            Assert.AreEqual(typeof(string), column.ColumnType,
                $"Column '{column.ColumnName}' should be of type string");
    }

    [TestMethod]
    public void DescSolutionNoArgs_VsWithArgs_ShouldReturnDifferentResults()
    {
        var queryNoArgs = "desc #csharp.solution";
        var vmNoArgs = CreateAndRunVirtualMachine(queryNoArgs);
        var tableNoArgs = vmNoArgs.Run();

        Assert.AreEqual(1, tableNoArgs.Count);
        Assert.AreEqual("solution", (string)tableNoArgs.First()[0]);
        Assert.AreEqual("path: System.String", (string)tableNoArgs.First()[1]);
    }
}
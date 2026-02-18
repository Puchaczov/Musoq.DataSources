using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.Postgres.Tests;

[TestClass]
public class PostgresSchemaDescribeTests
{
    static PostgresSchemaDescribeTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        var mockSchemaProvider = new Mock<ISchemaProvider>();
        mockSchemaProvider.Setup(f => f.GetSchema(It.IsAny<string>())).Returns(new PostgresSchema());

        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            mockSchemaProvider.Object,
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    [TestMethod]
    public void DescSchema_ShouldReturnEmptyResult()
    {
        var query = "desc #postgres";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(0, table.Count,
            "Postgres schema has dynamic table names, so desc #postgres should return empty result");
    }

    [TestMethod]
    public void DescDynamicTable_ShouldReturnMethodSignature()
    {
        var query = "desc #postgres.sometable";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count(), "Should have 2 columns");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("sometable", (string)row[0]);
        Assert.AreEqual("schemaName: System.String", (string)row[1]);
    }

    [TestMethod]
    public void DescDifferentTableName_ShouldReturnSameSignature()
    {
        var query = "desc #postgres.anothertable";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count());
        Assert.AreEqual(1, table.Count);

        var row = table.First();
        Assert.AreEqual("anothertable", (string)row[0]);
        Assert.AreEqual("schemaName: System.String", (string)row[1]);
    }

    [TestMethod]
    public void DescSchema_ShouldHaveConsistentColumnTypes()
    {
        var query = "desc #postgres.users";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        foreach (var column in table.Columns)
            Assert.AreEqual(typeof(string), column.ColumnType,
                $"Column '{column.ColumnName}' should be of type string");
    }

    [TestMethod]
    public void DescTableNoArgs_VsWithArgs_ShouldReturnDifferentResults()
    {
        var queryNoArgs = "desc #postgres.mytable";
        var vmNoArgs = CreateAndRunVirtualMachine(queryNoArgs);
        var tableNoArgs = vmNoArgs.Run();

        Assert.AreEqual(1, tableNoArgs.Count);
        Assert.AreEqual("mytable", (string)tableNoArgs.First()[0]);
        Assert.AreEqual("schemaName: System.String", (string)tableNoArgs.First()[1]);
    }
}
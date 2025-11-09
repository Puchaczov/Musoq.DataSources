using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Musoq.Schema;

namespace Musoq.DataSources.Sqlite.Tests;

[TestClass]
public class SqliteSchemaDescribeTests
{
    static SqliteSchemaDescribeTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        var mockSchemaProvider = new Mock<ISchemaProvider>();
        mockSchemaProvider.Setup(x => x.GetSchema(It.IsAny<string>()))
            .Returns(new SqliteSchema());

        var envVars = new Dictionary<string, string>
        {
            ["SQLITE_CONNECTION_STRING"] = "Data Source=:memory:"
        };

        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            mockSchemaProvider.Object,
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables(envVars));
    }

    [TestMethod]
    public void DescSchema_ShouldReturnEmptyForDynamicTables()
    {
        var query = "desc #sqlite";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(0, table.Count, "Should return empty result for dynamic tables schema");
    }

    [TestMethod]
    public void DescDynamicTable_ShouldReturnMethodSignature()
    {
        var query = "desc #sqlite.users";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count(), "Should have 1 column (Name)");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("users", (string)row[0]);
    }

    [TestMethod]
    public void DescAnotherDynamicTable_ShouldReturnMethodSignature()
    {
        var query = "desc #sqlite.products";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(1, table.Columns.Count(), "Should have 1 column (Name)");
        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("products", (string)row[0]);
    }

    [TestMethod]
    public void DescSchema_ShouldHaveConsistentColumnTypes()
    {
        var query = "desc #sqlite.orders";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        foreach (var column in table.Columns)
        {
            Assert.AreEqual(typeof(string), column.ColumnType,
                $"Column '{column.ColumnName}' should be of type string");
        }
    }

    [TestMethod]
    public void DescDifferentTables_ShouldReturnDifferentTableNames()
    {
        var query1 = "desc #sqlite.customers";
        var vm1 = CreateAndRunVirtualMachine(query1);
        var table1 = vm1.Run();

        var query2 = "desc #sqlite.orders";
        var vm2 = CreateAndRunVirtualMachine(query2);
        var table2 = vm2.Run();

        Assert.AreEqual(1, table1.Count);
        Assert.AreEqual(1, table2.Count);

        Assert.AreEqual("customers", (string)table1.First()[0]);
        Assert.AreEqual("orders", (string)table2.First()[0]);

        Assert.AreNotEqual(table1.First()[0], table2.First()[0],
            "Different table names should be returned");
    }
}

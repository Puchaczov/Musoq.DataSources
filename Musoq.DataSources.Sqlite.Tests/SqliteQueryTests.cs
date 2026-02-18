using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Sqlite.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

namespace Musoq.DataSources.Sqlite.Tests;

[TestClass]
public class SqliteQueryTests
{
    static SqliteQueryTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void WhenDescTable_ShouldReturnAllColumns()
    {
        const string script = "desc #sqlite.hello()";

        var vm = CreateAndRunVirtualMachineWithResponse(script);
        var table = vm.Run();

        Assert.AreEqual(2, table.Count);
        Assert.AreEqual("Key", table[0].Values[0]);
        Assert.AreEqual("Value", table[1].Values[0]);
    }

    [TestMethod]
    public void WhenAllRowsSelected_ShouldReturnAll()
    {
        const string script = "select Key, Value from #sqlite.hello()";

        var vm = CreateAndRunVirtualMachineWithResponse(script);
        var table = vm.Run();

        Assert.IsTrue(table.Count == 2, "Table should contain exactly 2 records");

        Assert.IsTrue(table.Any(r =>
                (string)r.Values[0] == "key1" &&
                (long)r.Values[1] == 1L),
            "Missing key1 record with value 1");

        Assert.IsTrue(table.Any(r =>
                (string)r.Values[0] == "key2" &&
                (long)r.Values[1] == 2L),
            "Missing key2 record with value 2");
    }

    [TestMethod]
    public void WhenOnlyFilteredRowsSelected_ShouldReturnSingle()
    {
        const string script = "select Key, Value from #sqlite.hello() where Key = 'key2'";

        var vm = CreateAndRunVirtualMachineWithResponse(script);
        var table = vm.Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("key2", table[0].Values[0]);
        Assert.AreEqual(2L, table[0].Values[1]);
    }

    [TestMethod]
    public void WhenUsingOwnColumnsToFilter_ShouldReturnFiltered()
    {
        const string script = "select Column1, Column2 from #sqlite.hello2() where Column1 > Column2";

        var vm = CreateAndRunVirtualMachineWithResponse(script);
        var table = vm.Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(4L, table[0].Values[0]);
        Assert.AreEqual(3L, table[0].Values[1]);
    }

    private static CompiledQuery CreateAndRunVirtualMachineWithResponse(string script)
    {
        var positionalEnvironmentVariables = new Dictionary<uint, IReadOnlyDictionary<string, string>>
        {
            {
                0, new Dictionary<string, string>
                {
                    { "SQLITE_CONNECTION_STRING", "Data Source=./Files/FirstExampleDatabase.db" }
                }
            }
        };

        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            new TestsSqliteSchemaProvider(),
            positionalEnvironmentVariables);
    }
}
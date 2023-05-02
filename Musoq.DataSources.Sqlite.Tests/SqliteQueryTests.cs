using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Converter;
using Musoq.DataSources.Sqlite.Tests.Components;
using Musoq.Evaluator;
using Musoq.Plugins;
using Musoq.Tests.Common;
using Environment = Musoq.Plugins.Environment;

namespace Musoq.DataSources.Sqlite.Tests;

[TestClass]
public class SqliteQueryTests
{
    [TestMethod]
    public void WhenDescTable_ShouldReturnAllColumns()
    {
        const string script = "desc #sqlite.hello()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script);
        var table = vm.Run();
        
        Assert.AreEqual(4, table.Count);
        Assert.AreEqual("Key", table[0].Values[0]);
        Assert.AreEqual("Key.Chars", table[1].Values[0]);
        Assert.AreEqual("Key.Length", table[2].Values[0]);
        Assert.AreEqual("Value", table[3].Values[0]);
    }
    
    [TestMethod]
    public void WhenAllRowsSelected_ShouldReturnAll()
    {
        const string script = "select Key, Value from #sqlite.hello()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script);
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
        Assert.AreEqual("key1", table[0].Values[0]);
        Assert.AreEqual(1L, table[0].Values[1]);
        Assert.AreEqual("key2", table[1].Values[0]);
        Assert.AreEqual(2L, table[1].Values[1]);
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
            // sqlite in-file database
            {0, new Dictionary<string, string>
                {
                    { "SQLITE_CONNECTION_STRING", "Data Source=./Files/FirstExampleDatabase.db" }
                }
            }
        };

        return InstanceCreator.CompileForExecution(
            script,
            Guid.NewGuid().ToString(), 
            new TestsSqliteSchemaProvider(), 
            positionalEnvironmentVariables);
    }

    static SqliteQueryTests()
    {
        new Environment().SetValue(Constants.NetStandardDllEnvironmentName, EnvironmentUtils.GetOrCreateEnvironmentVariable());

        Culture.ApplyWithDefaultCulture();
    }
}
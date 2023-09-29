using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Converter;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Musoq.Tests.Common;

namespace Musoq.DataSources.CANBus.Tests;

[TestClass]
public class WorkingWithDbcFilesTests
{
    [TestMethod]
    public void WhenOnlyEnginePropertiesMustBeUsed_ShouldFilterToOnlyEngine()
    {
        const string query = @"
select
    Timestamp,
    Message,
    Engine.Is_Turned_On,
    Engine.Oil_Temperature
from #can.separatedvalues('./Data/1/1.csv', './Data/1/1.dbc')
where Engine is not null";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
        
        Assert.AreEqual(0ul, table[0].Values[0]);
        Assert.IsNotNull(table[0].Values[1]);
        Assert.AreEqual(true, Convert.ToBoolean(table[0].Values[2]));
        Assert.AreEqual(90d, table[0].Values[3]);
        
        Assert.AreEqual(1ul, table[1].Values[0]);
        Assert.IsNotNull(table[1].Values[1]);
        Assert.AreEqual(false, Convert.ToBoolean(table[1].Values[2]));
        Assert.AreEqual(95d, table[1].Values[3]);
    }
    
    [TestMethod]
    public void WhenOnlyExhaustSystemPropertiesMustBeUsed_ShouldFilterToOnlyEngine()
    {
        const string query = @"
select
    Timestamp,
    Message,
    Exhaust_System.Exhaust_Gas_Temperature
from #can.separatedvalues('./Data/1/1.csv', './Data/1/1.dbc')
where Exhaust_System is not null";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual(2ul, table[0].Values[0]);
        Assert.IsNotNull(table[0].Values[1]);
        Assert.AreEqual(124d, table[0].Values[2]);
    }
    
    [TestMethod]
    public void WhenOnlyExhaustSystemPropertiesMustBeUsed_ShouldFilterToOnlyEngine2()
    {
        const string query = @"
select 
    m1.Name as MessageName,
    s1.Name as Signal1Name,
    s2.Name as Signal2Name,
    s1.StartBit as Signal1StartBit,
    s1.Length as Signal1Length,
    s2.StartBit as Signal2StartBit,
    s2.Length as Signal2Length
from 
    #can.messages('./Data/1/1.dbc') m1
inner join
    #can.signals('./Data/1/1.dbc') s1 on m1.ID = s1.ID
inner join
    #can.signals('./Data/1/1.dbc') s2 on m1.ID = s2.ID
where 
    m1.Name = s1.Name and
    m1.Name = s2.Name and
    s1.Name <> s2.Name and
    (
        (s1.StartBit >= s2.StartBit and s1.StartBit < s2.StartBit + s2.Length) or
        (s2.StartBit >= s1.StartBit and s2.StartBit < s1.StartBit + s1.Length)
    )";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual(2ul, table[0].Values[0]);
        Assert.IsNotNull(table[0].Values[1]);
        Assert.AreEqual(124d, table[0].Values[2]);
    }

    [TestMethod]
    public void WhenEncodedMessageMustBeDecoded_ShouldDecodeToTheSameValue()
    {
        var queryEncode = @"
select 
    messages.EncodeMessage('Exhaust_Gas_Temperature', 124) 
from #can.messages('./Data/1/1.dbc') messages where messages.Name = 'Exhaust_System'";
        
        var vm = CreateAndRunVirtualMachine(queryEncode);
        
        var table = vm.Run();
        
        var encodedValue = (byte[])table[0].Values[0];
        var encodedValueUlong = BitConverter.ToUInt64(encodedValue);
        
        var queryDecode = $@"
select 
    messages.DecodeMessage('Exhaust_Gas_Temperature', {encodedValueUlong})
from #can.messages('./Data/1/1.dbc') messages where messages.Name = 'Exhaust_System'";
        
        vm = CreateAndRunVirtualMachine(queryDecode);
        
        table = vm.Run();

        var decodedValue = BitConverter.ToDouble((byte[]) table[0].Values[0]);
        
        Assert.AreEqual(124d, decodedValue);
    }

    private static CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreator.CompileForExecution(script, Guid.NewGuid().ToString(), new CANBusSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    static WorkingWithDbcFilesTests()
    {
        new Plugins.Environment().SetValue(Constants.NetStandardDllEnvironmentName, EnvironmentUtils.GetOrCreateEnvironmentVariable());

        Culture.ApplyWithDefaultCulture();
    }
}
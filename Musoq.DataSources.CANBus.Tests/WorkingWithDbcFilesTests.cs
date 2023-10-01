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
    public void WhenDbcFileSignalsDoesNotOverlap_ShouldSucceed()
    {
        const string query = @"
select 
    1
from 
    #can.messages('./Data/2/2.dbc') m1
inner join
    #can.signals('./Data/2/2.dbc') s1 on m1.Name = s1.MessageName
inner join
    #can.signals('./Data/2/2.dbc') s2 on m1.Name = s2.MessageName
where
    s1.Name <> s2.Name and
    (
        s1.StartBit <= s2.StartBit + s2.Length and
        s1.StartBit + s1.Length >= s2.StartBit
    )";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(0, table.Count);
    }
    
    [TestMethod]
    public void WhenDbcSignalsOverlaps_S2InsideS1_ShouldDetectIt()
    {
        const string query = @"
select 
    s1.Name,
    s2.Name
from 
    #can.messages('./Data/3/3.dbc') m1
inner join
    #can.signals('./Data/3/3.dbc') s1 on m1.Name = s1.MessageName
inner join
    #can.signals('./Data/3/3.dbc') s2 on m1.Name = s2.MessageName
where
    s1.Name <> s2.Name and
    (
        s1.StartBit <= s2.StartBit + s2.Length and
        s1.StartBit + s1.Length >= s2.StartBit
    )";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
        
        Assert.AreEqual("Oil_Temperature", table[0].Values[0]);
        Assert.AreEqual("Is_Turned_On", table[0].Values[1]);
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

    [TestMethod]
    public void WhenMessageIsUnknown_ShouldBeAvailable()
    {
        const string query = @"
select
    Timestamp,
    Message,
    UnknownMessage.RawData
from #can.separatedvalues('./Data/1/1.csv', './Data/1/1.dbc')
where IsWellKnown = false";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual(3ul, table[0].Values[0]);
        Assert.IsNull(table[0].Values[1]);
        Assert.AreEqual(0x01ul, table[0].Values[2]);
    }

    [TestMethod]
    public void WhenMessageIsUnknownAndUnknownColumnAccessed_ShouldReturnNull()
    {
        const string query = @"
select
    Engine
from #can.separatedvalues('./Data/1/1.csv', './Data/1/1.dbc')
where IsWellKnown = false";
        
        var vm = CreateAndRunVirtualMachine(query);

        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.IsNull(table[0].Values[0]);
    }

    [TestMethod]
    public void WhenAllMessageColumnsAccessed_ShouldReturnValues()
    {
        const string query = @"
select
    ID,
    IsExtID,
    Name,
    DLC,
    Transmitter,
    Comment,
    CycleTime
from #can.messages('./Data/1/1.dbc')
where ID = 293";
        
        var vm = CreateAndRunVirtualMachine(query);

        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual(293u, table[0].Values[0]);
        Assert.AreEqual(false, table[0].Values[1]);
        Assert.AreEqual("Exhaust_System", table[0].Values[2]);
        Assert.AreEqual((ushort)1, table[0].Values[3]);
        Assert.AreEqual("Vector__XXX", table[0].Values[4]);
        Assert.AreEqual(null, table[0].Values[5]);
        Assert.AreEqual(0, table[0].Values[6]);
    }

    [TestMethod]
    public void WhenAllSignalColumnsAccessed_ShouldReturnValues()
    {
        const string query = @"
select
    ID,
    Name,
    StartBit,
    Length,
    ByteOrder,
    InitialValue,
    Factor,
    IsInteger,
    Offset,
    Minimum,
    Maximum,
    Unit,
    Receiver,
    Comment,
    Multiplexing,
    MessageName
from #can.signals('./Data/1/1.dbc')
where Name = 'Oil_Temperature'";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual(292u, table[0].Values[0]);
        Assert.AreEqual("Oil_Temperature", table[0].Values[1]);
        Assert.AreEqual((ushort)2, table[0].Values[2]);
        Assert.AreEqual((ushort)8, table[0].Values[3]);
        Assert.AreEqual((byte)1, table[0].Values[4]);
        Assert.AreEqual(0d, table[0].Values[5]);
        Assert.AreEqual(1d, table[0].Values[6]);
        Assert.AreEqual(true, table[0].Values[7]);
        Assert.AreEqual(0d, table[0].Values[8]);
        Assert.AreEqual(-50d, table[0].Values[9]);
        Assert.AreEqual(100d, table[0].Values[10]);
        Assert.AreEqual("CelciusDegree", table[0].Values[11]);
        Assert.AreEqual("Vector__XXX", ((string[])table[0].Values[12])[0]);
        Assert.AreEqual(null, table[0].Values[13]);
        Assert.AreEqual(string.Empty, table[0].Values[14]);
        Assert.AreEqual("Engine", table[0].Values[15]);
    }

    [TestMethod]
    public void WhenAllBaseColumnsAreAccessedForCsvFile_ShouldAllowAccessToAllColumns()
    {
        const string query = @"
select
    ID,
    Timestamp,
    Message,
    IsWellKnown
from #can.separatedvalues('./Data/1/1.csv', './Data/1/1.dbc')";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(4, table.Count);
        
        Assert.AreEqual(292u, table[0].Values[0]);
        Assert.AreEqual(0ul, table[0].Values[1]);
        Assert.IsNotNull(table[0].Values[2]);
        Assert.AreEqual(true, table[0].Values[3]);
        
        Assert.AreEqual(292u, table[1].Values[0]);
        Assert.AreEqual(1ul, table[1].Values[1]);
        Assert.IsNotNull(table[1].Values[2]);
        Assert.AreEqual(true, table[1].Values[3]);
        
        Assert.AreEqual(293u, table[2].Values[0]);
        Assert.AreEqual(2ul, table[2].Values[1]);
        Assert.IsNotNull(table[2].Values[2]);
        Assert.AreEqual(true, table[2].Values[3]);
        
        Assert.AreEqual(115u, table[3].Values[0]);
        Assert.AreEqual(3ul, table[3].Values[1]);
        Assert.IsNull(table[3].Values[2]);
        Assert.AreEqual(false, table[3].Values[3]);
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
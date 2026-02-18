using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

namespace Musoq.DataSources.CANBus.Tests;

[TestClass]
public class MessagesOrSignalsTests
{
    static MessagesOrSignalsTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void WhenDbcFileHasUnnecessarySpaceBeforeTheSemicolon_AndSignalsToRetrieve_ShouldSuccess()
    {
        const string query = "select Name from #can.signals('./Data/4/4.dbc')";

        var vm = CreateAndRunVirtualMachine(query);

        var table = vm.Run();

        Assert.IsTrue(table.Count == 3, "Table should have 3 entries");

        Assert.IsTrue(table.Any(row =>
            (string)row.Values[0] == "Exhaust_Gas_Temperature"
        ), "First entry should be Exhaust_Gas_Temperature");

        Assert.IsTrue(table.Any(row =>
            (string)row.Values[0] == "Oil_Temperature"
        ), "Second entry should be Oil_Temperature");

        Assert.IsTrue(table.Any(row =>
            (string)row.Values[0] == "Is_Turned_On"
        ), "Third entry should be Is_Turned_On");
    }

    [TestMethod]
    public void WhenSignalOrderRetrieved_ShouldSuccess()
    {
        const string query = "select MessageOrder, Name from #can.signals('./Data/1/1.dbc') s";

        var vm = CreateAndRunVirtualMachine(query);

        var table = vm.Run();

        Assert.IsTrue(table.Count == 3, "Table should contain exactly 3 records");

        Assert.IsTrue(table.Any(r =>
                (int)r.Values[0] == 0 && (string)r.Values[1] == "Exhaust_Gas_Temperature"),
            "Missing Exhaust Gas Temperature sensor record with index 0");

        Assert.IsTrue(table.Any(r =>
                (int)r.Values[0] == 0 && (string)r.Values[1] == "Oil_Temperature"),
            "Missing Oil Temperature sensor record with index 0");

        Assert.IsTrue(table.Any(r =>
                (int)r.Values[0] == 1 && (string)r.Values[1] == "Is_Turned_On"),
            "Missing Is Turned On sensor record with index 1");
    }

    [TestMethod]
    public void WhenDbcFileHasUnnecessarySpaceBeforeTheSemicolon_AndMessagesToRetrieve_ShouldSuccess()
    {
        const string query = "select Name from #can.messages('./Data/4/4.dbc')";

        var vm = CreateAndRunVirtualMachine(query);

        var table = vm.Run();

        Assert.IsTrue(table.Count == 2, "Table should contain exactly 2 records");

        Assert.IsTrue(table.Any(r => (string)r.Values[0] == "Exhaust_System"), "Missing Exhaust_System record");
        Assert.IsTrue(table.Any(r => (string)r.Values[0] == "Engine"), "Missing Engine record");
    }

    [TestMethod]
    public void WhenDescMessages_ShouldSucceed()
    {
        const string query = @"desc #can.messages('./Data/1/1.dbc')";

        var vm = CreateAndRunVirtualMachine(query);

        var table = vm.Run();

        Assert.IsTrue(table.Count > 7);

        var row = table.SingleOrDefault(f => f.Values[0].ToString() == "Id");
        Assert.IsNotNull(row);

        Assert.AreEqual(0, row.Values[1]);
        Assert.AreEqual("System.UInt32", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "IsExtId");
        Assert.IsNotNull(row);

        Assert.AreEqual(1, row.Values[1]);
        Assert.AreEqual("System.Boolean", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "Name");
        Assert.IsNotNull(row);

        Assert.AreEqual(2, row.Values[1]);
        Assert.AreEqual("System.String", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "DLC");
        Assert.IsNotNull(row);

        Assert.AreEqual(3, row.Values[1]);
        Assert.AreEqual("System.UInt16", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "Transmitter");
        Assert.IsNotNull(row);

        Assert.AreEqual(4, row.Values[1]);
        Assert.AreEqual("System.String", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "Comment");
        Assert.IsNotNull(row);

        Assert.AreEqual(5, row.Values[1]);
        Assert.AreEqual("System.String", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "CycleTime");
        Assert.IsNotNull(row);

        Assert.AreEqual(6, row.Values[1]);
        Assert.AreEqual("System.Int32", row.Values[2]);
    }

    [TestMethod]
    public void WhenDescSignals_ShouldSucceed()
    {
        const string query = @"desc #can.signals('./Data/1/1.dbc')";

        var vm = CreateAndRunVirtualMachine(query);

        var table = vm.Run();

        Assert.IsTrue(table.Count > 15);

        var row = table.SingleOrDefault(f => f.Values[0].ToString() == "Id");
        Assert.IsNotNull(row);

        Assert.AreEqual(0, row.Values[1]);
        Assert.AreEqual("System.UInt32", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "Name");
        Assert.IsNotNull(row);

        Assert.AreEqual(1, row.Values[1]);
        Assert.AreEqual("System.String", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "StartBit");
        Assert.IsNotNull(row);

        Assert.AreEqual(2, row.Values[1]);
        Assert.AreEqual("System.UInt16", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "Length");
        Assert.IsNotNull(row);

        Assert.AreEqual(3, row.Values[1]);
        Assert.AreEqual("System.UInt16", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "ByteOrder");
        Assert.IsNotNull(row);

        Assert.AreEqual(4, row.Values[1]);
        Assert.AreEqual("System.Byte", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "InitialValue");
        Assert.IsNotNull(row);

        Assert.AreEqual(5, row.Values[1]);
        Assert.AreEqual("System.Double", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "Factor");
        Assert.IsNotNull(row);

        Assert.AreEqual(6, row.Values[1]);
        Assert.AreEqual("System.Double", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "IsInteger");
        Assert.IsNotNull(row);

        Assert.AreEqual(7, row.Values[1]);
        Assert.AreEqual("System.Boolean", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "Offset");
        Assert.IsNotNull(row);

        Assert.AreEqual(8, row.Values[1]);
        Assert.AreEqual("System.Double", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "Minimum");
        Assert.IsNotNull(row);

        Assert.AreEqual(9, row.Values[1]);
        Assert.AreEqual("System.Double", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "Maximum");
        Assert.IsNotNull(row);

        Assert.AreEqual(10, row.Values[1]);
        Assert.AreEqual("System.Double", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "Unit");
        Assert.IsNotNull(row);

        Assert.AreEqual(11, row.Values[1]);
        Assert.AreEqual("System.String", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "Receiver");
        Assert.IsNotNull(row);

        Assert.AreEqual(12, row.Values[1]);
        Assert.AreEqual("System.String[]", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "Comment");
        Assert.IsNotNull(row);

        Assert.AreEqual(13, row.Values[1]);
        Assert.AreEqual("System.String", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "Multiplexing");
        Assert.IsNotNull(row);

        Assert.AreEqual(14, row.Values[1]);
        Assert.AreEqual("System.String", row.Values[2]);

        row = table.SingleOrDefault(f => f.Values[0].ToString() == "MessageName");
        Assert.IsNotNull(row);

        Assert.AreEqual(15, row.Values[1]);
        Assert.AreEqual("System.String", row.Values[2]);
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

        Assert.IsTrue(table.Count == 2, "Table should contain exactly 2 records");

        Assert.IsTrue(table.Any(r =>
                (string)r.Values[0] == "Oil_Temperature" &&
                (string)r.Values[1] == "Is_Turned_On"),
            "Missing record with Oil_Temperature and Is_Turned_On values");
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

        var queryDecode = $@"
select 
    messages.DecodeMessage('Exhaust_Gas_Temperature', {(ulong)table[0].Values[0]})
from #can.messages('./Data/1/1.dbc') messages where messages.Name = 'Exhaust_System'";

        vm = CreateAndRunVirtualMachine(queryDecode);

        table = vm.Run();

        var decodedValue = (double)table[0].Values[0];

        Assert.AreEqual(124d, decodedValue);
    }

    [TestMethod]
    public void WhenAllMessageColumnsAccessed_ShouldReturnValues()
    {
        const string query = @"
select
    Id,
    IsExtId,
    Name,
    DLC,
    Transmitter,
    Comment,
    CycleTime
from #can.messages('./Data/1/1.dbc')
where Id = 293";

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
    Id,
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
    public void WhenMessagesCrossAppliedWithSignals_ShouldPass()
    {
        const string query = "select m.Name, s.Name from #can.messages('./Data/1/1.dbc') m cross apply m.Signals s";

        var vm = CreateAndRunVirtualMachine(query);

        var table = vm.Run();

        Assert.IsTrue(table.Count == 3, "Table should contain exactly 3 records");

        Assert.IsTrue(table.Any(r =>
                (string)r.Values[0] == "Exhaust_System" &&
                (string)r.Values[1] == "Exhaust_Gas_Temperature"),
            "Missing Exhaust_System with Exhaust_Gas_Temperature record");

        Assert.IsTrue(table.Any(r =>
                (string)r.Values[0] == "Engine" &&
                (string)r.Values[1] == "Oil_Temperature"),
            "Missing Engine with Oil_Temperature record");

        Assert.IsTrue(table.Any(r =>
                (string)r.Values[0] == "Engine" &&
                (string)r.Values[1] == "Is_Turned_On"),
            "Missing Engine with Is_Turned_On record");
    }

    private static CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(), new CANBusSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }
}
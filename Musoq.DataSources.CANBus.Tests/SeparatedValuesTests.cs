using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

namespace Musoq.DataSources.CANBus.Tests;

[TestClass]
public class SeparatedValuesTests
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
from #can.separatedvalues('./Data/1/1.csv', './Data/1/1.dbc', 'dec', 'big')
where Engine is not null";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.IsTrue(table.Count == 2, "Table should contain exactly 2 records");

        Assert.IsTrue(table.Any(r => 
                (ulong)r.Values[0] == 0ul && 
                r.Values[1] != null && 
                Convert.ToBoolean(r.Values[2]) == true && 
                Math.Abs((double)r.Values[3] - 90d) < 0.0001),
            "Missing first sensor record");

        Assert.IsTrue(table.Any(r => 
                (ulong)r.Values[0] == 1ul && 
                r.Values[1] != null && 
                Convert.ToBoolean(r.Values[2]) == false && 
                Math.Abs((double)r.Values[3] - 95d) < 0.0001),
            "Missing second sensor record");
    }
    
    [TestMethod]
    public void WhenDataRetrieve_ShouldSuccess()
    {
        const string query = @"
select
    Data
from #can.separatedvalues('./Data/1/1.csv', './Data/1/1.dbc', 'dec', 'big')
where Engine is not null";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.IsTrue(table.Count == 2, "Table should have 2 entries");

        Assert.IsTrue(table.Any(row => 
            (ulong)row.Values[0] == 361ul
        ), "First entry should be 361");

        Assert.IsTrue(table.Any(row => 
            (ulong)row.Values[0] == 380ul
        ), "Second entry should be 380");
    }
    
    [TestMethod]
    public void WhenOnlyExhaustSystemPropertiesMustBeUsed_ShouldFilterToOnlyExhaustSystem()
    {
        const string query = @"
select
    Timestamp,
    Message,
    Exhaust_System.Exhaust_Gas_Temperature
from #can.separatedvalues('./Data/1/1.csv', './Data/1/1.dbc', 'dec', 'big')
where Exhaust_System is not null";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual(2ul, table[0].Values[0]);
        Assert.IsNotNull(table[0].Values[1]);
        Assert.AreEqual(124d, table[0].Values[2]);
    }

    [TestMethod]
    public void WhenMessageIsUnknown_ShouldBeAvailable()
    {
        const string query = @"
select
    Timestamp,
    Message,
    UnknownMessage.RawData
from #can.separatedvalues('./Data/1/1.csv', './Data/1/1.dbc', 'dec', 'big')
where IsWellKnown = false";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual(3ul, table[0].Values[0]);
        Assert.IsNull(table[0].Values[1]);
        Assert.AreEqual(72057594037927936ul, table[0].Values[2]);
    }

    [TestMethod]
    public void WhenMessageIsUnknownAndUnknownColumnAccessed_ShouldReturnNull()
    {
        const string query = @"
select
    Engine
from #can.separatedvalues('./Data/1/1.csv', './Data/1/1.dbc', 'dec', 'big')
where IsWellKnown = false";
        
        var vm = CreateAndRunVirtualMachine(query);

        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.IsNull(table[0].Values[0]);
    }

    [TestMethod]
    public void WhenAllBaseColumnsAreAccessedForCsvFile_ShouldAllowAccessToAllColumns()
    {
        const string query = @"
select
    ID,
    Timestamp,
    there ,
    IsWellKnown
from #can.separatedvalues('./Data/1/1.csv', './Data/1/1.dbc')";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.IsTrue(table.Count == 4, "Table should have 4 entries");

        Assert.IsTrue(table.Any(row => 
            (uint)row.Values[0] == 292u && 
            (ulong)row.Values[1] == 0ul && 
            row.Values[2] != null && 
            (bool)row.Values[3] == true
        ), "First entry should match 292, 0, non-null, true");

        Assert.IsTrue(table.Any(row => 
            (uint)row.Values[0] == 292u && 
            (ulong)row.Values[1] == 1ul && 
            row.Values[2] != null && 
            (bool)row.Values[3] == true
        ), "Second entry should match 292, 1, non-null, true");

        Assert.IsTrue(table.Any(row => 
            (uint)row.Values[0] == 293u && 
            (ulong)row.Values[1] == 2ul && 
            row.Values[2] != null && 
            (bool)row.Values[3] == true
        ), "Third entry should match 293, 2, non-null, true");

        Assert.IsTrue(table.Any(row => 
            (uint)row.Values[0] == 115u && 
            (ulong)row.Values[1] == 3ul && 
            row.Values[2] == null && 
            (bool)row.Values[3] == false
        ), "Fourth entry should match 115, 3, null, false");
    }

    [TestMethod]
    public void WhenNeedToRetrieveCanMessagesYoungerThan_ShouldSucceed()
    {
        const string query = @"
select ID from #can.separatedvalues('./Data/5/5.csv', './Data/5/5.dbc')
where FromTimestamp(Timestamp, 's') >= ToDateTimeOffset('2023-10-08T18:19:00','en-EN')
";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.IsTrue(table.Count == 2, "Table should have 2 entries");

        Assert.IsTrue(table.Any(row => 
            (uint)row.Values[0] == 293u
        ), "First entry should be 293");

        Assert.IsTrue(table.Any(row => 
            (uint)row.Values[0] == 115u
        ), "Second entry should be 115");
    }

    [TestMethod]
    public void WhenNeedToRetrieveCanMessagesOlderThan_ShouldSucceed()
    {
        const string query = @"
select ID from #can.separatedvalues('./Data/5/5.csv', './Data/5/5.dbc')
where FromTimestamp(Timestamp, 's') < ToDateTimeOffset('2023-10-08T18:19:00', 'en-EN')
";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.IsTrue(table.Count == 2, "Table should have 2 entries");

        Assert.IsTrue(table.Any(row => 
            (uint)row.Values[0] == 292u
        ), "Both entries should be 292");
    }
    
    [TestMethod]
    public void WhenDlcIsMissing_ShouldSucceed()
    {
        const string query = @"
select
    1
from #can.separatedvalues('./Data/6/6.csv', './Data/6/6.dbc')";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.IsTrue(table.Count == 4, "Table should have 4 entries");

        Assert.IsTrue(table.All(row => 
            (int)row.Values[0] == 1
        ), "All entries should be 1");
    }
    
    [TestMethod]
    public void WhenHexValuesInIdUsed_ShouldSuccess()
    {
        const string query = @"
select
    Timestamp,
    Message,
    Engine.Is_Turned_On,
    Engine.Oil_Temperature
from #can.separatedvalues('./Data/7/7.csv', './Data/7/7.dbc', 'hex', 'big')
where Engine is not null";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.IsTrue(table.Count == 2, "Table should have 2 entries");

        Assert.IsTrue(table.Any(row => 
            (ulong)row.Values[0] == 0ul && 
            row.Values[1] != null && 
            Convert.ToBoolean(row.Values[2]) && 
            Math.Abs((double)row.Values[3] - 90d) < 0.0001
        ), "First entry should match 0, non-null, true, 90");
    
        Assert.IsTrue(table.Any(row => 
            (ulong)row.Values[0] == 1ul && 
            row.Values[1] != null && 
            Convert.ToBoolean(row.Values[2]) == false &&
            Math.Abs((double)row.Values[3] - 95d) < 0.0001
        ), "Second entry should match 1, non-null, false, 95");
    }
    
    [TestMethod]
    public void WhenVehicleSpeedHasCertainValue_ShouldSucceed()
    {
        const string query = @"
select
    Driving.Vehicle_Speed
from #can.separatedvalues('./Data/8/8.csv', './Data/8/8.dbc', 'dec')";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual(25d, table[0].Values[0]);
    }
    
    [TestMethod]
    public void WhenVehicleHasIdAndSpeedOfCertainValue_ShouldSucceed()
    {
        const string query = @"
select
    ID,
    Driving.Vehicle_Speed
from #can.separatedvalues('./Data/9/9.csv', './Data/9/9.dbc', 'dec', 'big')";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual(444u, table[0].Values[0]);
        Assert.AreEqual(65.53d, table[0].Values[1]);
    }

    [TestMethod]
    public void WhenIsTurnedOnEncodedWithLittleEndian_ShouldSucceed()
    {
        const string query = @"
select
    m.DecodeMessage('Is_Turned_On', s.Data)
from #can.separatedvalues('./Data/10/10.csv', './Data/10/10.dbc', 'dec', 'little') s
inner join #can.messages('./Data/10/10.dbc') m on s.ID = m.Id";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.IsTrue(table.Count == 2, "Table should have 2 entries");

        Assert.IsTrue(table.Any(row => 
            (double)row.Values[0] == 0d
        ), "First entry should be 0");

        Assert.IsTrue(table.Any(row => 
            Math.Abs((double)row.Values[0] - 1d) < 0.0001
        ), "Second entry should be 1");
    }
    
    [TestMethod]
    public void WhenVehicleSpeedIsEncodedWithBigEndian_ShouldSucceed()
    {
        const string query = @"
select
    m.EncodeMessage('Vehicle_Speed', m.DecodeMessage('Vehicle_Speed', s.Data))
from #can.separatedvalues('./Data/11/11.csv', './Data/11/11.dbc', 'dec', 'big') s
inner join #can.messages('./Data/11/11.dbc') m on s.ID = m.Id";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);

        ulong hexValue = 0x0000009099010000;
        var bytes = BitConverter.GetBytes(hexValue);
        Array.Reverse(bytes);
        hexValue = BitConverter.ToUInt64(bytes, 0);
        
        Assert.AreEqual(hexValue, table[0].Values[0]);
    }

    private static CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(script, Guid.NewGuid().ToString(), new CANBusSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    static SeparatedValuesTests()
    {
        Culture.ApplyWithDefaultCulture();
    }
}
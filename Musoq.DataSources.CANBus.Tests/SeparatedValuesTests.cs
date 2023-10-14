using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Converter;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Musoq.Tests.Common;

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
    public void WhenDataRetrieve_ShouldSuccess()
    {
        const string query = @"
select
    Data
from #can.separatedvalues('./Data/1/1.csv', './Data/1/1.dbc')
where Engine is not null";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
        
        Assert.AreEqual(361ul, table[0].Values[0]);
        Assert.AreEqual(380ul, table[1].Values[0]);
    }
    
    [TestMethod]
    public void WhenOnlyExhaustSystemPropertiesMustBeUsed_ShouldFilterToOnlyExhaustSystem()
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
        Assert.AreEqual(72057594037927936ul, table[0].Values[2]);
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

    [TestMethod]
    public void WhenNeedToRetrieveCanMessagesYoungerThan_ShouldSucceed()
    {
        const string query = @"
select ID from #can.separatedvalues('./Data/5/5.csv', './Data/5/5.dbc')
where FromTimestamp(Timestamp, 's') >= ToDateTimeOffset('2023-10-08T18:19:00','yyyy-MM-ddTHH:mm:ss')
";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
        
        Assert.AreEqual(293u, table[0].Values[0]);
        Assert.AreEqual(115u, table[1].Values[0]);
    }

    [TestMethod]
    public void WhenNeedToRetrieveCanMessagesOlderThan_ShouldSucceed()
    {
        const string query = @"
select ID from #can.separatedvalues('./Data/5/5.csv', './Data/5/5.dbc')
where FromTimestamp(Timestamp, 's') < ToDateTimeOffset('2023-10-08T18:19:00','yyyy-MM-ddTHH:mm:ss')
";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
        
        Assert.AreEqual(292u, table[0].Values[0]);
        Assert.AreEqual(292u, table[1].Values[0]);
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
        
        Assert.AreEqual(4, table.Count);
        
        Assert.AreEqual(1, table[0].Values[0]);
        Assert.AreEqual(1, table[1].Values[0]);
        Assert.AreEqual(1, table[2].Values[0]);
        Assert.AreEqual(1, table[3].Values[0]);
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
from #can.separatedvalues('./Data/7/7.csv', './Data/7/7.dbc', 'hex')
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
    public void WhenVehicleSpeedHasCertainValue_ShouldSucceed()
    {
        const string query = @"
select
    Driving.Vehicle_Speed
from #can.separatedvalues('./Data/8/8.csv', './Data/8/8.dbc')";
        
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
from #can.separatedvalues('./Data/9/9.csv', './Data/9/9.dbc')";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        
        Assert.AreEqual(444u, table[0].Values[0]);
        Assert.AreEqual(65.53d, table[0].Values[1]);
    }

    private static CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreator.CompileForExecution(script, Guid.NewGuid().ToString(), new CANBusSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    static SeparatedValuesTests()
    {
        new Plugins.Environment().SetValue(Constants.NetStandardDllEnvironmentName, EnvironmentUtils.GetOrCreateEnvironmentVariable());

        Culture.ApplyWithDefaultCulture();
    }
}
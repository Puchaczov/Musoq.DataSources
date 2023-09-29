using System;
using System.Linq;
using DbcParserLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Converter;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Musoq.Tests.Common;

namespace Musoq.DataSources.CANBus.Tests;

[TestClass]
public class Class1
{
    [TestMethod]
    public void X()
    {
        const string query = @"
select
    Timestamp,
    Message,
    Engine.Is_Turned_On,
    Engine.Oil_Temperature
from #can.separatedvalues('./Data/1/1.csv', true, './Data/1/1.dbc')
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
    public void Y()
    {
        const string query = @"
select 
    Timestamp, 
    Message, 
    HVAC.Temperature, 
    HVAC.Mode 
from #can.separatedvalues('./Data/Motohawk/motohawk.csv', true, './Data/Motohawk/motohawk.dbc')
where HVAC is not null";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
    }

    [TestMethod]
    public void Z()
    {
        var dbc = DbcParserLib.Parser.ParseFromPath("./Data/Motohawk/motohawk.dbc");
        var message = dbc.Messages.SingleOrDefault(f => f.Name == "Engine");
        var signalTemperature = message.Signals.SingleOrDefault(f => f.Name == "Oil_Temperature");
        var signalIsTurnedOn = message.Signals.SingleOrDefault(f => f.Name == "Is_Turned_On");
        ulong value = 90;
        bool isTurnedOn = true;
        
        var pack = Packer.TxSignalPack(value, signalTemperature);
        pack |= Packer.TxSignalPack(isTurnedOn ? 1u : 0u, signalIsTurnedOn);
        var hex = pack.ToString("X");
    }

    [TestMethod]
    public void Z2()
    {
        var query = @"
select 
    messages.ToHex(messages.EncodeMessage('Exhaust_Gas_Temperature', messages.GetBytes(124))) 
from #can.messages('./Data/1/1.dbc') messages where messages.Name = 'Exhaust_System'";
        
        var vm = CreateAndRunVirtualMachine(query);
        
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
    }

    private static CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreator.CompileForExecution(script, Guid.NewGuid().ToString(), new CANBusSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    static Class1()
    {
        new Plugins.Environment().SetValue(Constants.NetStandardDllEnvironmentName, EnvironmentUtils.GetOrCreateEnvironmentVariable());

        Culture.ApplyWithDefaultCulture();
    }
}
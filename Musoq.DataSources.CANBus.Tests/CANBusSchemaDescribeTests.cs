using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.CANBus.Messages;
using Musoq.DataSources.CANBus.Signals;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

namespace Musoq.DataSources.CANBus.Tests;

[TestClass]
public class CANBusSchemaDescribeTests
{
    private CompiledQuery CreateAndRunVirtualMachine(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            new CANBusSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    static CANBusSchemaDescribeTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void DescSchema_ShouldListAllAvailableMethods()
    {
        var query = "desc #can";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(5, table.Columns.Count(), "Should have 5 columns: Name and up to 4 parameters");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);
        Assert.AreEqual("Param 1", table.Columns.ElementAt(2).ColumnName);
        Assert.AreEqual("Param 2", table.Columns.ElementAt(3).ColumnName);
        Assert.AreEqual("Param 3", table.Columns.ElementAt(4).ColumnName);

        Assert.AreEqual(5, table.Count, "Should have 5 rows (messages, signals, and 3 separatedvalues overloads)");

        var methodNames = table.Select(row => (string)row[0]).ToList();

        Assert.AreEqual(1, methodNames.Count(m => m == "messages"), "Should contain 'messages' method once");
        Assert.AreEqual(1, methodNames.Count(m => m == "signals"), "Should contain 'signals' method once");
        Assert.AreEqual(3, methodNames.Count(m => m == "separatedvalues"), "Should contain 'separatedvalues' method 3 times (3 overloads)");

        var messagesRow = table.First(row => (string)row[0] == "messages");
        Assert.AreEqual("dbc: System.String", (string)messagesRow[1]);
        Assert.IsNull(messagesRow[2]);

        var signalsRow = table.First(row => (string)row[0] == "signals");
        Assert.AreEqual("dbc: System.String", (string)signalsRow[1]);
        Assert.IsNull(signalsRow[2]);
    }

    [TestMethod]
    public void DescMessages_ShouldReturnMethodSignature()
    {
        var query = "desc #can.messages";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count(), "Should have 2 columns: Name and Param 0");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("messages", (string)row[0]);
        Assert.AreEqual("dbc: System.String", (string)row[1]);
    }

    [TestMethod]
    public void DescSignals_ShouldReturnMethodSignature()
    {
        var query = "desc #can.signals";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(2, table.Columns.Count(), "Should have 2 columns: Name and Param 0");
        Assert.AreEqual("Name", table.Columns.ElementAt(0).ColumnName);
        Assert.AreEqual("Param 0", table.Columns.ElementAt(1).ColumnName);

        Assert.AreEqual(1, table.Count, "Should have exactly 1 row");

        var row = table.First();
        Assert.AreEqual("signals", (string)row[0]);
        Assert.AreEqual("dbc: System.String", (string)row[1]);
    }

    [TestMethod]
    public void DescSeparatedValues_ShouldReturnAllOverloads()
    {
        var query = "desc #can.separatedvalues";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(5, table.Columns.Count(), "Should have 5 columns (Name, Param 0-3)");
        Assert.AreEqual(3, table.Count, "Should have 3 rows for 3 overloads");

        var methodNames = table.Select(row => (string)row[0]).ToList();
        Assert.IsTrue(methodNames.All(name => name == "separatedvalues"), "All rows should be for separatedvalues method");

        var overload1 = table.ElementAt(0);
        Assert.AreEqual("separatedvalues", (string)overload1[0]);
        Assert.AreEqual("csvData: System.String", (string)overload1[1]);
        Assert.AreEqual("dbcData: System.String", (string)overload1[2]);
        Assert.IsNull(overload1[3]);
        Assert.IsNull(overload1[4]);

        var overload2 = table.ElementAt(1);
        Assert.AreEqual("separatedvalues", (string)overload2[0]);
        Assert.AreEqual("csvData: System.String", (string)overload2[1]);
        Assert.AreEqual("dbcData: System.String", (string)overload2[2]);
        Assert.AreEqual("idOfType: System.String", (string)overload2[3]);
        Assert.IsNull(overload2[4]);

        var overload3 = table.ElementAt(2);
        Assert.AreEqual("separatedvalues", (string)overload3[0]);
        Assert.AreEqual("csvData: System.String", (string)overload3[1]);
        Assert.AreEqual("dbcData: System.String", (string)overload3[2]);
        Assert.AreEqual("idOfType: System.String", (string)overload3[3]);
        Assert.AreEqual("endianness: System.String", (string)overload3[4]);
    }

    [TestMethod]
    public void DescMessagesWithArgs_ShouldReturnTableSchema()
    {
        var query = "desc #can.messages('./Data/1/1.dbc')";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count());
        Assert.IsTrue(table.Count > 0, "Should have rows describing the table columns");

        var columnNames = table.Select(row => (string)row[0]).ToList();
        var expectedColumns = new[]
        {
            nameof(MessageEntity.Id),
            nameof(MessageEntity.IsExtId),
            nameof(MessageEntity.Name),
            nameof(MessageEntity.DLC),
            nameof(MessageEntity.Transmitter),
            nameof(MessageEntity.Comment),
            nameof(MessageEntity.CycleTime),
            nameof(MessageEntity.Signals)
        };

        foreach (var expectedColumn in expectedColumns)
        {
            Assert.IsTrue(columnNames.Contains(expectedColumn),
                $"Should have '{expectedColumn}' column");
        }
    }

    [TestMethod]
    public void DescSignalsWithArgs_ShouldReturnTableSchema()
    {
        var query = "desc #can.signals('./Data/1/1.dbc')";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        Assert.AreEqual(3, table.Columns.Count());
        Assert.IsTrue(table.Count > 0, "Should have rows describing the table columns");

        var columnNames = table.Select(row => (string)row[0]).ToList();
        var expectedColumns = new[]
        {
            nameof(SignalEntity.Id),
            nameof(SignalEntity.Name),
            nameof(SignalEntity.StartBit),
            nameof(SignalEntity.Length),
            nameof(SignalEntity.ByteOrder),
            nameof(SignalEntity.InitialValue),
            nameof(SignalEntity.Factor),
            nameof(SignalEntity.IsInteger),
            nameof(SignalEntity.Offset),
            nameof(SignalEntity.Minimum),
            nameof(SignalEntity.Maximum),
            nameof(SignalEntity.Unit),
            nameof(SignalEntity.Receiver),
            nameof(SignalEntity.Comment),
            nameof(SignalEntity.Multiplexing),
            nameof(SignalEntity.MessageName),
            nameof(SignalEntity.ValueMap),
            nameof(SignalEntity.MessageOrder)
        };

        foreach (var expectedColumn in expectedColumns)
        {
            Assert.IsTrue(columnNames.Contains(expectedColumn),
                $"Should have '{expectedColumn}' column");
        }
    }

    [TestMethod]
    public void DescUnknownMethod_ShouldThrowException()
    {
        var query = "desc #can.unknownmethod";

        try
        {
            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();
            Assert.Fail("Should have thrown an exception for unknown method");
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            Assert.IsTrue(
                message.Contains("unknownmethod", StringComparison.OrdinalIgnoreCase),
                $"Error message should mention the unknown method. Got: {message}");
            Assert.IsTrue(
                message.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Available data sources", StringComparison.OrdinalIgnoreCase),
                $"Error message should be helpful. Got: {message}");
        }
    }

    [TestMethod]
    public void DescSchema_ShouldHaveConsistentColumnTypes()
    {
        var query = "desc #can";

        var vm = CreateAndRunVirtualMachine(query);
        var table = vm.Run();

        foreach (var column in table.Columns)
        {
            Assert.AreEqual(typeof(string), column.ColumnType,
                $"Column '{column.ColumnName}' should be of type string");
        }
    }

    [TestMethod]
    public void DescMessagesNoArgs_VsWithArgs_ShouldReturnDifferentResults()
    {
        var queryNoArgs = "desc #can.messages";
        var vmNoArgs = CreateAndRunVirtualMachine(queryNoArgs);
        var tableNoArgs = vmNoArgs.Run();

        var queryWithArgs = "desc #can.messages('./Data/1/1.dbc')";
        var vmWithArgs = CreateAndRunVirtualMachine(queryWithArgs);
        var tableWithArgs = vmWithArgs.Run();

        Assert.AreNotEqual(tableNoArgs.Count, tableWithArgs.Count,
            "Method signature vs table schema should have different row counts");

        Assert.AreEqual(1, tableNoArgs.Count);
        Assert.AreEqual("messages", (string)tableNoArgs.First()[0]);

        Assert.IsTrue(tableWithArgs.Count > 1);
        var columnNames = tableWithArgs.Select(row => (string)row[0]).ToList();
        Assert.IsTrue(columnNames.Contains(nameof(MessageEntity.Id)));
        Assert.IsTrue(columnNames.Contains(nameof(MessageEntity.Name)));
    }
}

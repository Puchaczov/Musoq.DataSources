using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.CANBus.Tests;

[TestClass]
public sealed class CANBusStarContractTests
{
    private static readonly StarContractCase[] Cases =
    [
        new(
            "messages",
            [typeof(string)],
            ["./Data/1/1.dbc"],
            "select * from can.messages('./Data/1/1.dbc')",
            [
                Column("Id", typeof(uint)), Column("IsExtId", typeof(bool)), Column("Name", typeof(string)),
                Column("DLC", typeof(ushort)), Column("Transmitter", typeof(string)), Column("Comment", typeof(string)),
                Column("CycleTime", typeof(int))
            ],
            ["Signals"]),
        new(
            "signals",
            [typeof(string)],
            ["./Data/1/1.dbc"],
            "select * from can.signals('./Data/1/1.dbc')",
            [
                Column("Id", typeof(uint)), Column("Name", typeof(string)), Column("StartBit", typeof(ushort)),
                Column("Length", typeof(ushort)), Column("ByteOrder", typeof(byte)), Column("InitialValue", typeof(double)),
                Column("Factor", typeof(double)), Column("IsInteger", typeof(bool)), Column("Offset", typeof(double)),
                Column("Minimum", typeof(double)), Column("Maximum", typeof(double)), Column("Unit", typeof(string)),
                Column("Comment", typeof(string)), Column("Multiplexing", typeof(string)), Column("MessageName", typeof(string)),
                Column("MessageOrder", typeof(int))
            ],
            ["Receiver", "ValueMap"])
    ];

    [TestMethod]
    public void EveryConcreteCANConstructor_HasOneExactStarContract()
    {
        var schema = new CANBusSchema();
        var context = CreateMetadataContext();

        foreach (var contract in Cases)
        {
            StarContractAssertions.AssertConstructors(
                schema.GetRawConstructors(contract.MethodName, context),
                [contract]);

            var table = schema.GetTableByName(contract.MethodName, context, contract.Arguments.ToArray());
            StarContractAssertions.AssertExcludedColumnsRemainInSchema(table, contract);

            var result = Compile(contract.Query).Run();
            StarContractAssertions.AssertResult(result, contract);
        }
    }

    [TestMethod]
    public void SignalValueMap_IsCrossApplyAddressable()
    {
        var query = "select v.Value, v.Name from can.signals('./Data/4/4.dbc') s cross apply s.ValueMap v";
        var result = Compile(query).Run();

        Assert.IsNotNull(result);
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            new CANBusSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static SourceMetadataContext CreateMetadataContext()
    {
        return new SourceMetadataContext(
            "can-star-contract",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            NullLogger.Instance);
    }

    private static StarContractColumn Column(string name, Type type) => new(name, type);
}

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

namespace Musoq.DataSources.Time.Tests;

[TestClass]
public sealed class TimeStarContractTests
{
    private static readonly StarContractCase[] Cases =
    [
        new(
            "interval",
            [typeof(string), typeof(string), typeof(string)],
            ["01.04.2018 00:00:00", "03.04.2018 00:00:00", "days"],
            "select * from time.interval('01.04.2018 00:00:00', '03.04.2018 00:00:00', 'days')",
            [
                Column("DateTime", typeof(DateTimeOffset)),
                Column("Second", typeof(int)),
                Column("Minute", typeof(int)),
                Column("Hour", typeof(int)),
                Column("Day", typeof(int)),
                Column("Month", typeof(int)),
                Column("Year", typeof(int)),
                Column("DayOfWeek", typeof(int)),
                Column("DayOfYear", typeof(int))
            ],
            ["TimeOfDay"])
    ];

    [TestMethod]
    public void EveryTimeConstructor_HasOneExactStarContract()
    {
        var schema = new TimeSchema();
        var context = CreateMetadataContext();

        StarContractAssertions.AssertConstructors(schema.GetRawConstructors(context), Cases);

        foreach (var contract in Cases)
        {
            var table = schema.GetTableByName(contract.MethodName, context, contract.Arguments.ToArray());
            StarContractAssertions.AssertExcludedColumnsRemainInSchema(table, contract);

            var result = Compile(contract.Query).Run();
            StarContractAssertions.AssertResult(result, contract);
        }
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            new TimeSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static SourceMetadataContext CreateMetadataContext()
    {
        return new SourceMetadataContext(
            "time-star-contract",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            NullLogger.Instance);
    }

    private static StarContractColumn Column(string name, Type type) => new(name, type);
}

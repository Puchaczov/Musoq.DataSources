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

namespace Musoq.DataSources.System.Tests;

[TestClass]
public sealed class SystemStarContractTests
{
    private static readonly StarContractCase[] Cases =
    [
        new(
            "dual",
            [],
            [],
            "select * from system.dual()",
            [Column("Dummy", typeof(string))],
            []),
        new(
            "range",
            [typeof(long)],
            [3L],
            "select * from system.range(3l)",
            [Column("Value", typeof(long))],
            []),
        new(
            "range",
            [typeof(long), typeof(long)],
            [1L, 3L],
            "select * from system.range(1l, 3l)",
            [Column("Value", typeof(long))],
            [])
    ];

    [TestMethod]
    public void EverySystemConstructor_HasOneExactStarContract()
    {
        var schema = new SystemSchema();
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
            new SystemSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static SourceMetadataContext CreateMetadataContext()
    {
        return new SourceMetadataContext(
            "system-star-contract",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            NullLogger.Instance);
    }

    private static StarContractColumn Column(string name, Type type) => new(name, type);
}

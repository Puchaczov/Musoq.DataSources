using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.FlatFile;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.Schema.FlatFile.Tests;

[TestClass]
public sealed class FlatFileStarContractTests
{
    private static readonly StarContractCase[] Cases =
    [
        new(
            "file",
            [typeof(string)],
            ["./TestMultilineFile.txt"],
            "select * from flat.file('./TestMultilineFile.txt')",
            [
                Column("LineNumber", typeof(int)),
                Column("Line", typeof(string))
            ],
            [])
    ];

    [TestMethod]
    public void EveryFlatFileConstructor_HasOneExactStarContract()
    {
        var schema = new FlatFileSchema();
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
            new FlatFileSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static SourceMetadataContext CreateMetadataContext()
    {
        return new SourceMetadataContext(
            "flat-file-star-contract",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            NullLogger.Instance);
    }

    private static StarContractColumn Column(string name, Type type) => new(name, type);
}

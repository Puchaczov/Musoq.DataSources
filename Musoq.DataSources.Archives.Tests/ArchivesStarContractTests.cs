using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpCompress.Common;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Archives.Tests;

[TestClass]
public sealed class ArchivesStarContractTests
{
    private static readonly StarContractCase[] Cases =
    [
        new(
            "file",
            [typeof(string)],
            ["./Files/Example1/archives.zip"],
            "select * from archives.file('./Files/Example1/archives.zip')",
            [
                Column(
                    "CompressionType",
                    typeof(int),
                    typeof(CompressionType),
                    EnumTypeOrigin.NativeClr,
                    typeof(CompressionType).FullName!,
                    EnumUnderlyingKind.Int32,
                    false),
                Column("ArchivedTime", typeof(DateTime?)),
                Column("CompressedSize", typeof(long)),
                Column("Crc", typeof(long)),
                Column("CreatedTime", typeof(DateTime?)),
                Column("Key", typeof(string)),
                Column("LinkTarget", typeof(string)),
                Column("IsDirectory", typeof(bool)),
                Column("IsEncrypted", typeof(bool)),
                Column("IsSplitAfter", typeof(bool)),
                Column("IsSolid", typeof(bool)),
                Column("VolumeIndexFirst", typeof(int)),
                Column("VolumeIndexLast", typeof(int)),
                Column("LastAccessedTime", typeof(DateTime?)),
                Column("LastModifiedTime", typeof(DateTime?)),
                Column("Size", typeof(long)),
                Column("Attrib", typeof(int?))
            ],
            [])
    ];

    [TestMethod]
    public void EveryArchiveConstructor_HasOneExactStarContract()
    {
        var schema = new ArchivesSchema();
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
            new ArchivesSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static SourceMetadataContext CreateMetadataContext()
    {
        return new SourceMetadataContext(
            "archives-star-contract",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            NullLogger.Instance);
    }

    private static StarContractColumn Column(string name, Type type) => new(name, type);

    private static StarContractColumn Column(
        string name,
        Type type,
        Type schemaSourceReadType,
        EnumTypeOrigin enumOrigin,
        string enumDisplayName,
        EnumUnderlyingKind enumUnderlyingKind,
        bool enumIsFlags) =>
        new(
            name,
            type,
            schemaSourceReadType,
            enumOrigin,
            enumDisplayName,
            enumUnderlyingKind,
            enumIsFlags);
}

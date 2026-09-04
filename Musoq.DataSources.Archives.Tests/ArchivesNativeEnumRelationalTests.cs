#nullable enable

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

namespace Musoq.DataSources.Archives.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ArchivesNativeEnumRelationalTests
{
    [TestMethod]
    public void CompressionType_RelationalShapesPreserveCarrierMetadataAndValues()
    {
        var queries = new[]
        {
            "with entries as (select e.Key, e.CompressionType from archives.file('./Files/Example1/archives.zip') e) " +
            "select entries.CompressionType from entries where entries.CompressionType = 'None'",
            "select e.Kind from (select a.CompressionType as Kind from archives.file('./Files/Example1/archives.zip') a) e",
            "select leftEntry.CompressionType from archives.file('./Files/Example1/archives.zip') leftEntry " +
            "inner join archives.file('./Files/Example1/archives.zip') rightEntry " +
            "on leftEntry.CompressionType = rightEntry.CompressionType where leftEntry.Key = 'text1.txt'",
            "select e.CompressionType, Count(e.Key) as Total " +
            "from archives.file('./Files/Example1/archives.zip') e group by e.CompressionType",
            "select distinct e.CompressionType from archives.file('./Files/Example1/archives.zip') e",
            "select e.CompressionType from archives.file('./Files/Example1/archives.zip') e " +
            "union all (CompressionType) select f.CompressionType from archives.file('./Files/Example1/archives.zip') f",
            "select e.CompressionType, RowNumber() over (partition by e.CompressionType order by e.Key) as RowNo " +
            "from archives.file('./Files/Example1/archives.zip') e order by e.Key"
        };

        foreach (var query in queries)
        {
            using var table = Run(query);
            var enumColumn = table.Columns.SingleOrDefault(column => column.EnumType is not null);
            Assert.IsNotNull(enumColumn, $"No enum metadata for query: {query}");
            Assert.AreEqual(typeof(int), enumColumn!.ColumnType, query);
            Assert.AreEqual(typeof(int), enumColumn.SourceReadType, query);
            Assert.IsTrue(table.Count > 0, query);
            Assert.IsTrue(table.SelectMany(static row => row.Values).All(static value => value is not Enum), query);
        }
    }

    [TestMethod]
    public void CompressionType_RelationalAliasesDoNotLeakClrEnums()
    {
        using var table = Run(
            "select e.CompressionType as Kind, e.Key as Entry " +
            "from archives.file('./Files/Example1/archives.zip') e " +
            "order by Entry");

        PortableEnumAssertions.AssertEnumColumn(
            table,
            "Kind",
            typeof(int),
            typeof(int),
            typeof(SharpCompress.Common.CompressionType),
            Musoq.Schema.EnumUnderlyingKind.Int32,
            isFlags: false);
        PortableEnumAssertions.AssertNoClrEnumValues(table);
    }

    private static Musoq.Evaluator.Tables.Table Run(string query)
    {
        var compiled = InstanceCreatorHelpers.CompileForExecution(
            query,
            $"ArchivesNativeEnumRelational_{Guid.NewGuid():N}",
            new ArchivesSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        return compiled.Run();
    }
}

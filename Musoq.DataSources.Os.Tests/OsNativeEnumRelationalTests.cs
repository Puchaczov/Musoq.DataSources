#nullable enable

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Os.Compare.Directories;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Tests;

[TestClass]
[DoNotParallelize]
public sealed class OsNativeEnumRelationalTests
{
    private const string CompareSource =
        "os.dirscompare('./Directories/Directory1', './Directories/Directory2')";

    [TestMethod]
    public void State_RelationalShapesPreserveCarrierMetadataAndValues()
    {
        var queries = new[]
        {
            $"with states as (select c.State, c.SourceFileRelative from {CompareSource} c) " +
            "select s.State from states s where s.State = 'Added'",
            $"select s.Kind from (select c.State as Kind from {CompareSource} c) s",
            $"select leftState.State from {CompareSource} leftState " +
            $"inner join {CompareSource} rightState on leftState.State = rightState.State " +
            "where leftState.State = 'Added'",
            $"select leftState.State from {CompareSource} leftState " +
            $"left outer join {CompareSource} rightState on leftState.SourceFileRelative = rightState.SourceFileRelative",
            $"select c.State, Count(c.SourceFileRelative) as Total from {CompareSource} c group by c.State",
            $"select distinct c.State from {CompareSource} c",
            $"select c.State from {CompareSource} c union all (State) select d.State from {CompareSource} d",
            $"select c.State, RowNumber() over (partition by c.State order by c.DestinationFileRelative) as RowNo " +
            $"from {CompareSource} c order by RowNo"
        };

        foreach (var query in queries)
        {
            using var table = Run(query);
            var enumColumn = table.Columns.SingleOrDefault(column => column.EnumType is not null);
            if (query.Contains("left outer join", StringComparison.Ordinal))
            {
                // A nullable outer-join projection deliberately drops the
                // nominal descriptor while retaining the primitive carrier.
                var outerColumn = table.Columns.Single(column => column.ColumnName.EndsWith(".State", StringComparison.Ordinal));
                Assert.AreEqual(typeof(int), outerColumn.ColumnType, query);
                Assert.AreEqual(typeof(int), outerColumn.SourceReadType, query);
            }
            else
            {
                Assert.IsNotNull(enumColumn, $"No enum metadata for query: {query}");
                Assert.AreEqual(typeof(int), enumColumn!.ColumnType, query);
                Assert.AreEqual(typeof(int), enumColumn.SourceReadType, query);
            }
            Assert.IsTrue(table.Count > 0, query);
            Assert.IsTrue(table.SelectMany(static row => row.Values).All(static value => value is not Enum), query);
        }
    }

    [TestMethod]
    public void State_CteAndDerivedProjectionRetainStateDescriptor()
    {
        using var table = Run(
            $"with states as (select c.State as ComparisonState from {CompareSource} c) " +
            "select s.ComparisonState from states s where s.ComparisonState in ('Added', 'Removed')");

        PortableEnumAssertions.AssertEnumColumn(
            table,
            "s.ComparisonState",
            typeof(int),
            typeof(int),
            typeof(State),
            EnumUnderlyingKind.Int32,
            isFlags: false,
            ["TheSame", "Modified", "Added", "Removed"]);
        PortableEnumAssertions.AssertNoClrEnumValues(table);
        Assert.IsTrue(table.All(row =>
        {
            var value = (int)row[0];
            return value == (int)State.Added || value == (int)State.Removed;
        }));
    }

    private static Musoq.Evaluator.Tables.Table Run(string query)
    {
        var compiled = InstanceCreatorHelpers.CompileForExecution(
            query,
            $"OsNativeEnumRelational_{Guid.NewGuid():N}",
            new OsSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        return compiled.Run();
    }
}

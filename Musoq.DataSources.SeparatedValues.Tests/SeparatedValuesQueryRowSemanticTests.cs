#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SeparatedValuesQueryRowSemanticTests
{
    [TestMethod]
    public void CompiledQueries_NativeRows_PreserveRelationalOperatorSemantics()
    {
        WithCsv(
            "Name,Team,Age\nAda,A,36\nGrace,A,36\nLinus,B,29\nMargaret,C,50\n",
            path =>
            {
                var source = $"separatedvalues.comma('{QueryPath(path)}', true, 0)";
                var scenarios = new (string Name, string Query, string[] Expected)[]
                {
                    (
                        "predicate-skip-take",
                        $"select d.Name, d.Age from {source} d where d.Age >= 30 order by d.Age, d.Name skip 1 take 2",
                        ["Grace|36", "Margaret|50"]),
                    (
                        "aggregation-grouping",
                        $"select d.Team, Count(d.Name) as Count from {source} d group by d.Team order by d.Team",
                        ["A|2", "B|1", "C|1"]),
                    (
                        "window",
                        $"select d.Name, RowNumber() over (order by d.Age, d.Name) as RowNo from {source} d order by RowNo",
                        ["Linus|1", "Ada|2", "Grace|3", "Margaret|4"]),
                    (
                        "cte",
                        $"with adults as (select d.Name as Name, d.Age as Age from {source} d where d.Age >= 30) " +
                        "select a.Name, a.Age from adults a order by a.Name",
                        ["Ada|36", "Grace|36", "Margaret|50"]),
                    (
                        "inner-join",
                        $"select l.Name, r.Name from {source} l inner join {source} r on l.Team = r.Team " +
                        "where l.Name = 'Ada' and r.Name = 'Grace'",
                        ["Ada|Grace"]),
                    (
                        "left-outer-join",
                        $"select l.Name, r.Name from {source} l left outer join {source} r " +
                        "on l.Team = r.Team and r.Name = 'missing' order by l.Name",
                        ["Ada|<null>", "Grace|<null>", "Linus|<null>", "Margaret|<null>"]),
                    (
                        "set-operation",
                        $"select d.Name as Name from {source} d where d.Team = 'A' " +
                        $"union all (Name) select e.Name as Name from {source} e where e.Team = 'B' order by Name",
                        ["Ada", "Grace", "Linus"])
                };

                foreach (var scenario in scenarios)
                {
                    var queryRows = Run(scenario.Query);

                    CollectionAssert.AreEqual(
                        scenario.Expected,
                        queryRows,
                        $"query-row result for {scenario.Name}");
                }
            });
    }

    [TestMethod]
    public void CompiledQuery_WhenTypedTableIsCoupled_MaterializesDeclaredTypes()
    {
        WithCsv("Name,Amount\nAda,12\nBob,3\n", path =>
        {
            var query =
                "table CsvShape { Name: string, Amount: int };" +
                "couple separatedvalues.comma with table CsvShape as Rows;" +
                $"select Name, Amount from Rows('{QueryPath(path)}', true, 0) where Amount > 5 order by Name";

            CollectionAssert.AreEqual(new[] { "Ada|12" }, Run(query));
        });
    }

    [TestMethod]
    public void CompiledQuery_WhenCoupledProjectionStartsAfterColumnZero_MapsDescriptorOrdinalsByName()
    {
        WithCsv("Id,Quantity,Price,Category,Note\n1,2,12.5,A,first\n2,3,7.25,B,second\n", path =>
        {
            var query =
                "table CsvShape { Id: long, Quantity: long, Price: decimal, Category: string, Note: string };" +
                "couple separatedvalues.comma with table CsvShape as Rows;" +
                $"select Quantity, Price from Rows('{QueryPath(path)}', true, 0) order by Quantity";

            CollectionAssert.AreEqual(new[] { "2|12.5", "3|7.25" }, Run(query));
        });
    }

    [TestMethod]
    public void CompiledQuery_WhenHeaderless_UsesOneBasedColumnNames()
    {
        WithCsv("Ada,Open\nGrace,Closed\n", path =>
        {
            var query =
                $"select d.Column2, d.Column1 from separatedvalues.comma('{QueryPath(path)}', false, 0) d " +
                "order by d.Column1";

            CollectionAssert.AreEqual(
                new[] { "Open|Ada", "Closed|Grace" },
                Run(query));
        });
    }

    [TestMethod]
    public void CompiledQuery_ExplicitEnumTable_ReadsPrimitiveCarrierAndNames()
    {
        WithCsv("Status\nRunning\n10\n99\n", path =>
        {
            var query =
                "enum StatusKind : int { Queued = 10, Running = 20 };" +
                "table CsvShape { Status: StatusKind };" +
                "couple separatedvalues.comma with table CsvShape as Rows;" +
                $"select Status, EnumName(Status) from Rows('{QueryPath(path)}', true, 0)";

            using var compiled = InstanceCreatorHelpers.CompileForExecution(
                query,
                $"SeparatedValuesEnumSemantic_{Guid.NewGuid():N}",
                new CsvSchemaProvider(),
                EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
            using var table = compiled.Run();

            var statusColumn = table.Columns.ElementAt(0);
            Assert.AreEqual(typeof(int?), statusColumn.ColumnType);
            Assert.AreEqual(typeof(int?), statusColumn.SourceReadType);
            Assert.IsNotNull(statusColumn.EnumType);
            Assert.IsFalse(table.SelectMany(static row => row.Values).Any(static value => value is Enum));
            CollectionAssert.AreEqual(
                new[] { "20|Running", "10|Queued", "99|<null>" },
                table.Select(row => string.Join(
                    "|",
                    Enumerable.Range(0, row.Count).Select(index => row[index]?.ToString() ?? "<null>")))
                    .ToArray());
        });
    }

    [TestMethod]
    public void CompiledQuery_ExplicitEnumPredicates_PushDownAndPreserveSqlNullSemantics()
    {
        WithCsv(
            "Status,Access\n" +
            "20,3\n" +
            "10,1\n" +
            "99,3\n" +
            ",3\n",
            path =>
            {
                var query =
                    "enum StatusKind : int { Queued = 10, Running = 20, Finished = 30 };" +
                    "flags enum AccessKind : uint { None = 0ui, Read = 1ui, Write = 2ui, ReadWrite = 3ui };" +
                    "table CsvShape { Status: StatusKind, Access: AccessKind };" +
                    "couple separatedvalues.comma with table CsvShape as Rows;" +
                    $"select Status, EnumName(Status) from Rows('{QueryPath(path)}', true, 0) " +
                    "where Status <> 'Finished' " +
                    "and Status not in ('Queued', 'Running') " +
                    "and Access is not null " +
                    "and HasAllFlags(Access, 'Read', 'Write')";

                CollectionAssert.AreEqual(new[] { "99|<null>" }, Run(query));
            });
    }

    [TestMethod]
    public void CompiledQuery_InvalidEnumValueReportsBoundedDescriptorContext()
    {
        WithCsv("Status\n" + new string('x', 160) + "\n", path =>
        {
            var query =
                "enum StatusKind : int { Queued = 10, Running = 20 };" +
                "table CsvShape { Status: StatusKind };" +
                "couple separatedvalues.comma with table CsvShape as Rows;" +
                $"select Status from Rows('{QueryPath(path)}', true, 0)";

            using var compiled = InstanceCreatorHelpers.CompileForExecution(
                query,
                $"SeparatedValuesEnumDiagnostic_{Guid.NewGuid():N}",
                new CsvSchemaProvider(),
                EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
            using var table = compiled.Run();
            var exception = Assert.Throws<Exception>(() => _ = table[0]);

            StringAssert.Contains(exception.ToString(), "enum 'StatusKind'");
            StringAssert.Contains(exception.ToString(), "Int32");
            StringAssert.Contains(exception.ToString(), "column 'Status'");
            StringAssert.Contains(exception.ToString(), "observed '");
            var diagnostic = exception.ToString();
            var observedStart = diagnostic.IndexOf("observed '", StringComparison.Ordinal);
            Assert.IsTrue(observedStart >= 0);
            var observedEnd = diagnostic.IndexOf("'", observedStart + "observed '".Length,
                StringComparison.Ordinal);
            Assert.IsTrue(observedEnd > observedStart && observedEnd - observedStart <= 110,
                "Enum diagnostics must bound the observed value.");
        });
    }

    [TestMethod]
    public void CompiledQuery_MalformedUtf8ReportsSourceAndRowContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-query-enum-invalid-{Guid.NewGuid():N}.csv");
        var header = Encoding.UTF8.GetBytes("Status\n");
        var bytes = new byte[header.Length + 3];
        Buffer.BlockCopy(header, 0, bytes, 0, header.Length);
        bytes[^3] = 0xc3;
        bytes[^2] = 0x28;
        bytes[^1] = (byte)'\n';
        File.WriteAllBytes(path, bytes);

        try
        {
            var query =
                "enum StatusKind : int { Queued = 10, Running = 20 };" +
                "table CsvShape { Status: StatusKind };" +
                "couple separatedvalues.comma with table CsvShape as Rows;" +
                $"select Status from Rows('{QueryPath(path)}', true, 0)";
            using var compiled = InstanceCreatorHelpers.CompileForExecution(
                query,
                $"SeparatedValuesEnumUtf8Diagnostic_{Guid.NewGuid():N}",
                new CsvSchemaProvider(),
                EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
            using var table = compiled.Run();
            var exception = Assert.Throws<Exception>(() => _ = table[0]);
            StringAssert.Contains(exception.ToString(), "row 1");
            StringAssert.Contains(exception.ToString(), "<malformed UTF-8 field>");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void CompiledQuery_ExplicitEnumTable_HeaderlessCustomDelimiterUsesCarrierMetadata()
    {
        WithCsv("20|10|99\n", path =>
        {
            var query =
                "enum StatusKind : ushort { Queued = 10us, Running = 20us };" +
                "table CsvShape { Column1: StatusKind, Column2: StatusKind, Column3: StatusKind };" +
                "couple separatedvalues.delimited with table CsvShape as Rows;" +
                $"select Column1, EnumName(Column1), Column3 from Rows('{QueryPath(path)}', '|', false, 0)";

            using var compiled = InstanceCreatorHelpers.CompileForExecution(
                query,
                $"SeparatedValuesEnumDelimited_{Guid.NewGuid():N}",
                new CsvSchemaProvider(),
                EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
            using var table = compiled.Run();

            Assert.AreEqual(typeof(ushort?), table.Columns.ElementAt(0).ColumnType);
            Assert.IsNotNull(table.Columns.ElementAt(0).EnumType);
            CollectionAssert.AreEqual(
                new[] { "20|Running|99" },
                table.Select(row => string.Join(
                    "|",
                    Enumerable.Range(0, row.Count).Select(index => row[index]?.ToString() ?? "<null>")))
                    .ToArray());
            Assert.IsFalse(table.SelectMany(static row => row.Values).Any(static value => value is Enum));
        });
    }

    [TestMethod]
    public void CompiledQuery_EnumCarrierSurvivesRelationalShapes()
    {
        WithCsv("Name,Status\nAda,10\nGrace,10\nLinus,20\n", path =>
        {
            var prefix =
                "enum StatusKind : int { Queued = 10, Running = 20 };" +
                "table CsvShape { Name: string, Status: StatusKind };" +
                "couple separatedvalues.comma with table CsvShape as Rows;";
            var queries = new[]
            {
                prefix + $"select d.Status, Count(d.Name) as Count from Rows('{QueryPath(path)}', true, 0) d " +
                "group by d.Status order by Count",
                prefix + $"with statuses as (select d.Status as Status, d.Name as Name from Rows('{QueryPath(path)}', true, 0) d) " +
                "select s.Status, s.Name from statuses s order by s.Name",
                prefix + $"select l.Name, r.Name from Rows('{QueryPath(path)}', true, 0) l " +
                "inner join " + $"Rows('{QueryPath(path)}', true, 0) r on l.Status = r.Status " +
                "where l.Name = 'Ada' and r.Name = 'Grace'",
                prefix + $"select d.Name, d.Status, RowNumber() over (partition by d.Status order by d.Name) as RowNo " +
                $"from Rows('{QueryPath(path)}', true, 0) d order by d.Name"
            };

            foreach (var query in queries)
            {
                using var compiled = InstanceCreatorHelpers.CompileForExecution(
                    query,
                    $"SeparatedValuesEnumRelational_{Guid.NewGuid():N}",
                    new CsvSchemaProvider(),
                    EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
                using var table = compiled.Run();
                Assert.IsFalse(table.SelectMany(static row => row.Values).Any(static value => value is Enum));
                Assert.IsTrue(table.Columns.Any(column => column.EnumType is not null) ||
                              query.Contains("l.Name, r.Name", StringComparison.Ordinal));
            }
        });
    }

    private static string[] Run(string query)
    {
        using var compiled = InstanceCreatorHelpers.CompileForExecution(
            query,
            $"SeparatedValuesSemantic_{Guid.NewGuid():N}",
            new CsvSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        using var table = compiled.Run();

        return table
            .Select(static row => string.Join(
                "|",
                Enumerable.Range(0, row.Count).Select(index => Format(row[index]))))
            .ToArray();
    }

    private static string Format(object? value)
    {
        return value switch
        {
            null => "<null>",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string QueryPath(string path)
    {
        return Path.GetFullPath(path)
            .Replace('\\', '/')
            .Replace("'", "''", StringComparison.Ordinal);
    }

    private static void WithCsv(string contents, Action<string> assertion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-query-semantics-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, contents, new UTF8Encoding(false, true));
        try
        {
            assertion(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

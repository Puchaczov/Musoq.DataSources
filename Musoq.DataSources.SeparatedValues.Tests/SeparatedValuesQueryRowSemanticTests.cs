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

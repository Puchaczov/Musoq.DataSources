using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Structured;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json.Tests;

[TestClass]
public class JsonStreamingExecutionTests
{
    [TestMethod]
    public void Query_WhenNumericPredicateIsAccepted_FiltersInsideSource()
    {
        WithJson("[{\"Id\":1},{\"Id\":2},{\"Id\":3},{\"Id\":4}]", path =>
        {
            var table = Compile($"select Id from json.file('{QueryPath(path)}') where Id >= 3").Run();

            CollectionAssert.AreEqual(new object[] { 3L, 4L }, table.Select(row => row[0]).ToArray());
        });
    }

    [TestMethod]
    public void Query_WhenEscapedStringPredicateIsAccepted_UsesDecodedOrdinalValue()
    {
        WithJson("[{\"Name\":\"A\\u0064a\"},{\"Name\":\"Grace\"}]", path =>
        {
            var table = Compile($"select Name from json.file('{QueryPath(path)}') where Name = 'Ada'").Run();

            Assert.AreEqual(1, table.Count);
            Assert.AreEqual("Ada", table[0][0]);
        });
    }

    [TestMethod]
    public void Query_WhenPropertyNameIsEscaped_BindsWithoutChangingItsName()
    {
        WithJson("[{\"\\u004eame\":\"Ada } \\\"Lovelace\\\"\"}]", path =>
        {
            var table = Compile($"select Name from json.file('{QueryPath(path)}')").Run();

            Assert.AreEqual("Ada } \"Lovelace\"", table[0][0]);
        });
    }

    [TestMethod]
    public void Query_WhenUnselectedValueCrossesInputBuffers_SkipsItWithoutChangingProjection()
    {
        var payload = new string('x', 2 * 1024 * 1024) + " } ] \\\" still text";
        WithJson($"[{{\"Payload\":\"{payload.Replace("\\", "\\\\").Replace("\"", "\\\"")}\",\"Id\":7}}]", path =>
        {
            var table = Compile($"select Id from json.file('{QueryPath(path)}')").Run();

            Assert.AreEqual(1, table.Count);
            Assert.AreEqual(7L, table[0][0]);
        });
    }

    [TestMethod]
    public void Query_WhenRowsAreRandomAndSparse_MatchesSystemTextJsonReference()
    {
        WithGeneratedJson(1_000, path =>
        {
            var table = Compile(
                    $"select Id, Text, Score from json.file('{QueryPath(path)}') where Id >= 500")
                .Run();
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var expected = document.RootElement.EnumerateArray()
                .Where(row => row.GetProperty("Id").GetInt64() >= 500)
                .Select(row => new object[]
                {
                    row.GetProperty("Id").GetInt64(),
                    row.TryGetProperty("Text", out var text) ? text.GetString() : null,
                    row.TryGetProperty("Score", out var score) ? score.GetDecimal() : null
                })
                .ToArray();

            Assert.AreEqual(expected.Length, table.Count);
            for (var index = 0; index < expected.Length; index++)
                CollectionAssert.AreEqual(expected[index], table[index].Values.ToArray());
        });
    }

    [TestMethod]
    public void JsonSource_WhenTakeIsAccepted_StopsFramingAtRequestedRow()
    {
        WithJson("[{\"Id\":1},{\"Id\":2},{\"Id\":3},{\"Id\":4}]", path =>
        {
            var schema = new JsonSchema();
            var request = Request([new SourceColumnRef("Id")], take: 2);
            var plan = schema.TryPlanSource("file", request, path).ExecutionPlan;
            var capture = new DataSourceProgressCapture();
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Id", 0, typeof(long))],
                executionPlan: plan,
                dataSourceProgressCallback: capture.Handler);

            var rows = new JsonSource(path, context).Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual(2L, capture.For("json", DataSourcePhase.RowsRead).Single().RowsProcessed);
            Assert.AreEqual(4L, capture.For("json", DataSourcePhase.RowsKnown).Single().TotalRows);
        });
    }

    [TestMethod]
    public void JsonSource_WhenProjectionHasNoColumns_EmitsOneEmptyRowPerRecord()
    {
        WithJson("[{\"Id\":1},{\"Id\":2},{\"Id\":3}]", path =>
        {
            var plan = new SourceExecutionPlan
            {
                Identity = new SourceIdentity("json", "file", "zero", "source"),
                AcceptedColumns = [],
                AcceptedOrderBy = [],
                Properties = new Dictionary<string, object>
                {
                    [JsonPlanning.ReadPlanPropertyName] = new JsonReadPlan(true)
                }
            };
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                executionPlan: plan);

            var chunks = new JsonSource(path, context).Chunks.ToArray();
            var rows = chunks.SelectMany(chunk => chunk).ToArray();

            Assert.IsTrue(chunks.Length > 0);
            Assert.IsTrue(chunks.All(chunk =>
                chunk is RowChunk<object[]> { Source: RepeatedValueChunk<object[]> }));
            Assert.AreEqual(3, rows.Length);
            Assert.IsTrue(rows.All(row => row.Length == 0));
            Assert.IsTrue(rows.All(row => ReferenceEquals(row, Array.Empty<object>())));
        });
    }

    [TestMethod]
    public void JsonSource_WhenUnescapedStringRepeats_ReusesSnapshotStringInstance()
    {
        WithJson("[{\"Name\":\"Station-A\"},{\"Name\":\"Station-A\"}]", path =>
        {
            var plan = new JsonSchema()
                .TryPlanSource("file", Request([new SourceColumnRef("Name")]), path)
                .ExecutionPlan;
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Name", 0, typeof(string))],
                executionPlan: plan);
            var rows = new JsonSource(path, context).Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(2, rows.Length);
            Assert.AreSame(rows[0][0], rows[1][0]);
        });
    }

    [TestMethod]
    public void JsonSource_WhenZeroColumnProjectionHasPredicate_StillParsesAndFiltersRows()
    {
        WithJson("[{\"Id\":1},{\"Id\":2},{\"Id\":3}]", path =>
        {
            var plan = new JsonSchema()
                .TryPlanSource(
                    "file",
                    Request([], predicate: GreaterThan("Id", 1L)),
                    path)
                .ExecutionPlan;
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [],
                executionPlan: plan);
            var rows = new JsonSource(path, context).Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(2, rows.Length);
            Assert.IsTrue(rows.All(row => ReferenceEquals(row, Array.Empty<object>())));
        });
    }

    [TestMethod]
    public void JsonSource_WhenExplicitConversionFails_ThrowsInsteadOfReturningNull()
    {
        WithJson("{\"Value\":\"not-a-number\"}", path =>
        {
            var plan = new SourceExecutionPlan
            {
                Identity = new SourceIdentity("json", "file", "conversion", "source"),
                AcceptedColumns = [new SourceColumnRef("Value")],
                AcceptedOrderBy = []
            };
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Value", 0, typeof(decimal))],
                executionPlan: plan);
            var source = new JsonSource(path, context);

            var exception = Assert.ThrowsExactly<FormatException>(() =>
                source.Chunks.SelectMany(chunk => chunk).ToArray());

            StringAssert.Contains(exception.Message, "Value");
        });
    }

    [TestMethod]
    public void Query_WhenNestedValueIsSelected_MaterializesOnlyThatContainer()
    {
        WithJson("{\"Id\":1,\"Payload\":{\"Name\":\"Ada\",\"Values\":[1,2,3]}}", path =>
        {
            var table = Compile($"select Payload from json.file('{QueryPath(path)}')").Run();
            var payload = (Dictionary<string, object>)table[0][0];

            Assert.AreEqual("Ada", payload["Name"]);
            CollectionAssert.AreEqual(new object[] { 1L, 2L, 3L }, (List<object>)payload["Values"]);
        });
    }

    [TestMethod]
    public void Query_WhenColumnKindsConflict_MaterializesEachNaturalScalarAsObject()
    {
        WithJson("[{\"Value\":1},{\"Value\":\"one\"},{\"Value\":true}]", path =>
        {
            var table = Compile($"select Value from json.file('{QueryPath(path)}')").Run();

            CollectionAssert.AreEqual(new object[] { 1L, "one", true }, table.Select(row => row[0]).ToArray());
        });
    }

    [TestMethod]
    public void Query_WhenNumericKindsAreInferred_MaterializesContractTypes()
    {
        WithJson("{\"Integer\":1,\"Fraction\":1.25,\"Exponent\":1e2}", path =>
        {
            var table = Compile(
                    $"select Integer, Fraction, Exponent from json.file('{QueryPath(path)}')")
                .Run();

            Assert.IsInstanceOfType<long>(table[0][0]);
            Assert.IsInstanceOfType<decimal>(table[0][1]);
            Assert.IsInstanceOfType<double>(table[0][2]);
        });
    }

    private static SourcePlanRequest Request(
        IReadOnlyList<SourceColumnRef> columns,
        long? take = null,
        SourcePredicateExpression predicate = null)
    {
        return new SourcePlanRequest
        {
            Identity = new SourceIdentity("json", "file", Guid.NewGuid().ToString("N"), "source"),
            RequiredColumns = columns,
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = [],
            Skip = null,
            Take = take
        };
    }

    private static SourcePredicateComparison GreaterThan(string columnName, object value)
    {
        return new SourcePredicateComparison(
            SourcePredicateComparisonOperator.GreaterThan,
            new SourcePredicateColumn(new SourceColumnRef(columnName)),
            new SourcePredicateLiteral(value));
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            $"JsonStreaming_{Guid.NewGuid():N}",
            new JsonSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static string QueryPath(string path)
    {
        return Path.GetFullPath(path).Replace('\\', '/').Replace("'", "''", StringComparison.Ordinal);
    }

    private static void WithGeneratedJson(int rowCount, Action<string> assertion)
    {
        var path = TempPath();
        try
        {
            using (var stream = File.Create(path))
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartArray();
                for (var row = 0; row < rowCount; row++)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("Id", row);
                    if (row % 3 != 0)
                        writer.WriteString("Text", row % 7 == 0 ? $"row-}}-\\\"-{row}" : $"row-{row}");
                    if (row % 5 != 0)
                        writer.WriteNumber("Score", row / 10m);
                    if ((row & 1) == 0)
                        writer.WriteBoolean("Active", true);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            assertion(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void WithJson(string json, Action<string> assertion)
    {
        var path = TempPath();
        File.WriteAllText(path, json, new UTF8Encoding(false));
        try
        {
            assertion(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string TempPath()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-json-stream-{Guid.NewGuid():N}.json");
    }
}

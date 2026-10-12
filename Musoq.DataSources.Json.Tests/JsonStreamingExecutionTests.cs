using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Structured;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Converter;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json.Tests;

[TestClass]
public class JsonStreamingExecutionTests
{
    [TestMethod]
    public void Query_WhenTypedNullableValuesFeedBoundedWindows_ShouldRetainExactTypesAndEmptyFrameResults()
    {
        WithJson("[{\"Id\":1,\"Amount\":1.25},{\"Id\":2,\"Amount\":null},{\"Id\":3,\"Amount\":2.5}]", path =>
        {
            var query = $"select Id, Sum(Amount) over (order by Id rows between 1 preceding and current row) as Total, " +
                        $"Count(Amount) over (order by Id rows between 1 following and 1 following) as Following " +
                        $"from json.file('{QueryPath(path)}') order by Id";
            using var compiled = Compile(query);
            var table = compiled.Run();
            var rows = table.ToArray();
            var columns = table.Columns.ToArray();
            CollectionAssert.AreEqual(new[] { "Id", "Total", "Following" }, columns.Select(column => column.ColumnName).ToArray());
            CollectionAssert.AreEqual(new[] { typeof(long), typeof(decimal?), typeof(long) }, columns.Select(column => column.ColumnType).ToArray());
            Assert.AreEqual(3, rows.Length);
            CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, rows.Select(row => row.GetValue<long>(0)).ToArray());
            CollectionAssert.AreEqual(new decimal?[] { 1.25m, 1.25m, 2.5m }, rows.Select(row => row.GetValue<decimal?>(1)).ToArray());
            CollectionAssert.AreEqual(new long[] { 0, 1, 0 }, rows.Select(row => row.GetValue<long>(2)).ToArray());
        });
    }

    [TestMethod]
    public void Query_WhenConcreteNumericRowsFeedGroupedFunctions_UsesTypedSourceTransfer()
    {
        WithJson("[{\"Team\":\"A\",\"Amount\":1.25},{\"Team\":\"A\",\"Amount\":2.5},{\"Team\":\"B\",\"Amount\":null}]", path =>
        {
            var query = $"select Team, Sum(Amount) as Total, Count(Amount) as Present " +
                        $"from json.file('{QueryPath(path)}') group by Team order by Team";
            using var compiled = Compile(query);
            var table = compiled.Run();
            Assert.AreEqual(2, table.Count);
            CollectionAssert.AreEqual(new[] { "Team", "Total", "Present" },
                table.Columns.Select(column => column.ColumnName).ToArray());
            var columns = table.Columns.ToArray();
            Assert.AreEqual(typeof(decimal?), columns[1].ColumnType);
            Assert.AreEqual(typeof(long), columns[2].ColumnType);
            Assert.AreEqual("A", table[0].GetValue<string>(0));
            Assert.AreEqual(3.75m, table[0].GetValue<decimal?>(1));
            Assert.AreEqual(2L, table[0].GetValue<long>(2));
            Assert.AreEqual("B", table[1].GetValue<string>(0));
            Assert.IsNull(table[1].GetValue<decimal?>(1));
            Assert.AreEqual(0L, table[1].GetValue<long>(2));

            var inspected = InstanceCreator.CompileForInspection(query, $"JsonTyped_{Guid.NewGuid():N}",
                new JsonSchemaProvider(), new TypedNullLoggerResolver(),
                new CompilationOptions(ParallelizationMode.Full, usePrimitiveTypeValidation: true));
            StringAssert.Contains(inspected.GeneratedCSharpCode, "QuerySourceInvocation<");
            StringAssert.Contains(inspected.GeneratedCSharpCode, "Read<decimal?>");
            Assert.IsFalse(inspected.GeneratedCSharpCode.Contains("GetRowSource<object", StringComparison.Ordinal));
        });
    }

    private sealed class TypedNullLoggerResolver : ILoggerResolver
    {
        public ILogger ResolveLogger() => NullLogger.Instance;
        public ILogger<T> ResolveLogger<T>() => NullLogger<T>.Instance;
    }

    [TestMethod]
    public void Query_WhenNumericPredicateIsAccepted_FiltersInsideSource()
    {
        WithJson("[{\"Id\":1},{\"Id\":2},{\"Id\":3},{\"Id\":4}]", path =>
        {
            var table = Compile($"select Id from json.file('{QueryPath(path)}') where Id >= 3").Run();

            CollectionAssert.AreEqual(new object[] { 3L, 4L }, table.Select(row => row.ReadValue(0)).ToArray());
        });
    }

    [TestMethod]
    public void Query_WhenEscapedStringPredicateIsAccepted_UsesDecodedOrdinalValue()
    {
        WithJson("[{\"Name\":\"A\\u0064a\"},{\"Name\":\"Grace\"}]", path =>
        {
            var table = Compile($"select Name from json.file('{QueryPath(path)}') where Name = 'Ada'").Run();

            Assert.AreEqual(1, table.Count);
            Assert.AreEqual("Ada", table[0].ReadValue(0));
        });
    }

    [TestMethod]
    public void Query_WhenPropertyNameIsEscaped_BindsWithoutChangingItsName()
    {
        WithJson("[{\"\\u004eame\":\"Ada } \\\"Lovelace\\\"\"}]", path =>
        {
            var table = Compile($"select Name from json.file('{QueryPath(path)}')").Run();

            Assert.AreEqual("Ada } \"Lovelace\"", table[0].ReadValue(0));
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
            Assert.AreEqual(7L, table[0].ReadValue(0));
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
                CollectionAssert.AreEqual(expected[index], table[index].ReadValues().ToArray());
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

            var rows = new NativeJsonFixtureSource(path, context).Chunks.SelectMany(chunk => chunk).ToArray();

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

            var chunks = new NativeJsonFixtureSource(path, context).Chunks.ToArray();
            var rows = chunks.SelectMany(chunk => chunk).ToArray();

            Assert.IsTrue(chunks.Length > 0);
            Assert.IsTrue(chunks.All(chunk =>
                chunk is NativeQueryRowFixtures.FixtureChunk observed &&
                observed.StorageType.GetGenericTypeDefinition() == typeof(RepeatedValueChunk<>)));
            Assert.AreEqual(3, rows.Length);
            Assert.IsTrue(rows.All(row => row.Length == 0));
            Assert.IsTrue(rows.All(row => ReferenceEquals(row, rows[0])));
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
            var rows = new NativeJsonFixtureSource(path, context).Chunks.SelectMany(chunk => chunk).ToArray();

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
            var rows = new NativeJsonFixtureSource(path, context).Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(2, rows.Length);
            Assert.IsTrue(rows.All(row => ReferenceEquals(row, rows[0])));
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
            var source = new NativeJsonFixtureSource(path, context);

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
            var plan = new JsonSchema()
                .TryPlanSource("file", Request([new SourceColumnRef("Payload")]), path)
                .ExecutionPlan;
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Payload", 0, typeof(object))],
                executionPlan: plan);
            var rows = new NativeJsonFixtureSource(path, context).Chunks.SelectMany(chunk => chunk).ToArray();
            var payload = rows[0].GetValue<JsonElement>(0);

            Assert.AreEqual("Ada", payload.GetProperty("Name").GetString());
            CollectionAssert.AreEqual(new long[] { 1L, 2L, 3L },
                payload.GetProperty("Values").EnumerateArray().Select(static value => value.GetInt64()).ToArray());
        });
    }

    [TestMethod]
    public void Query_WhenColumnKindsConflict_MaterializesEachScalarAsConcreteJsonElement()
    {
        WithJson("[{\"Value\":1},{\"Value\":\"one\"},{\"Value\":true}]", path =>
        {
            var plan = new JsonSchema()
                .TryPlanSource("file", Request([new SourceColumnRef("Value")]), path)
                .ExecutionPlan;
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Value", 0, typeof(object))],
                executionPlan: plan);
            var rows = new NativeJsonFixtureSource(path, context).Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(3, rows.Length);
            Assert.AreEqual(1L, rows[0].GetValue<JsonElement>(0).GetInt64());
            Assert.AreEqual("one", rows[1].GetValue<JsonElement>(0).GetString());
            Assert.IsTrue(rows[2].GetValue<JsonElement>(0).GetBoolean());
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

            Assert.IsInstanceOfType<long>(table[0].ReadValue(0));
            Assert.IsInstanceOfType<decimal>(table[0].ReadValue(1));
            Assert.IsInstanceOfType<double>(table[0].ReadValue(2));
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

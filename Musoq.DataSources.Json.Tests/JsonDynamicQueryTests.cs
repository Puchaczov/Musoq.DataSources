#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Converter.Exceptions;
using Musoq.DataSources.Tests.Common;
using Musoq.DataSources.Structured;
using Musoq.Evaluator;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json.Tests;

[TestClass]
public class JsonDynamicQueryTests
{
    [TestMethod]
    public void Query_WhenRootIsOneObject_ReturnsOneRow()
    {
        WithJson("{\"Name\":\"Ada\",\"Age\":36}", path =>
        {
            var table = Compile($"select Name, Age from json.file('{QueryPath(path)}')").Run();

            Assert.AreEqual(1, table.Count);
            Assert.AreEqual("Ada", table[0].ReadValue(0));
            Assert.AreEqual(36L, table[0].ReadValue(1));
        });
    }

    [TestMethod]
    public void Query_WhenRowsAreSparse_ReturnsNullForMissingValues()
    {
        WithJson("[{\"Id\":1},{\"Id\":2,\"Late\":\"yes\"}]", path =>
        {
            var table = Compile($"select Id, Late from json.file('{QueryPath(path)}') order by Id").Run();

            Assert.AreEqual(typeof(long), table.Columns.ElementAt(0).ColumnType);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(1).ColumnType);
            Assert.AreEqual(2, table.Count);
            Assert.IsNull(table[0].ReadValue(1));
            Assert.AreEqual("yes", table[1].ReadValue(1));
        });
    }

    [TestMethod]
    public void Query_WhenColumnDoesNotExist_FailsCompilation()
    {
        WithJson("{\"Name\":\"Ada\"}", path =>
        {
            var exception = Assert.ThrowsExactly<MusoqQueryException>(() =>
                Compile($"select Missing from json.file('{QueryPath(path)}')"));

            StringAssert.Contains(FlattenMessages(exception), "Missing");
            Assert.IsInstanceOfType<StructuredUnknownColumnException>(exception.GetBaseException());
        });
    }

    [TestMethod]
    public void Query_WhenColumnCaseDoesNotMatch_FailsCompilation()
    {
        WithJson("{\"Name\":\"Ada\"}", path =>
        {
            var exception = Assert.ThrowsExactly<MusoqQueryException>(() =>
                Compile($"select name from json.file('{QueryPath(path)}')"));

            StringAssert.Contains(FlattenMessages(exception), "name");
            Assert.IsInstanceOfType<StructuredUnknownColumnException>(exception.GetBaseException());
        });
    }

    [TestMethod]
    public void Query_WhenBoundTypeChangesBeforeExecution_FailsWithSchemaDrift()
    {
        WithJson("{\"Value\":1}", path =>
        {
            var schema = new JsonSchema();
            var request = new SourcePlanRequest
            {
                Identity = new SourceIdentity("json", "file", "drift-test", "source"),
                RequiredColumns = [new SourceColumnRef("Value")],
                SourceRuntimeSettings = new Dictionary<string, string>(),
                Predicate = null,
                OrderBy = [],
                Skip = null,
                Take = null
            };
            var plan = schema.TryPlanSource("file", request, path).ExecutionPlan;
            File.WriteAllText(path, "{\"Value\":\"changed\"}", new UTF8Encoding(false));
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Value", 0, typeof(long))],
                executionPlan: plan);
            var source = new NativeJsonFixtureSource(path, context);

            var exception = Assert.ThrowsExactly<StructuredSchemaDriftException>(() =>
                source.Chunks.SelectMany(chunk => chunk).ToArray());

            StringAssert.Contains(FlattenMessages(exception), "changed incompatibly after binding");
        });
    }

    [TestMethod]
    public void Schema_WhenStreamParameterIsUsed_RejectsIt()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        var context = RuntimeV2TestContexts.CreateExecutionContext();

        Assert.ThrowsExactly<ArgumentException>(() =>
            global::Musoq.DataSources.Tests.Common.NativeSourceCalls.Source(new JsonSchema(), global::Musoq.Schema.SourceArguments.Create(stream, global::Musoq.Schema.SourceArguments.Empty)).Read<object[]>("file", context));
    }

    [TestMethod]
    public void Query_WhenNestedPayloadIsSelected_ConsumesConcreteJsonElement()
    {
        WithJson("{\"Id\":1,\"Payload\":{\"Name\":\"Ada\",\"Values\":[1,2,3]}}", path =>
        {
            var table = Compile($"select JsonText(Payload) as Payload from json.file('{QueryPath(path)}')").Run();
            Assert.AreEqual(1, table.Count);
            Assert.AreEqual("Payload", table.Columns.Single().ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.Single().ColumnType);
            using var document = System.Text.Json.JsonDocument.Parse(table[0].GetValue<string>(0));
            var payload = document.RootElement;
            Assert.AreEqual("Ada", payload.GetProperty("Name").GetString());
            CollectionAssert.AreEqual(new long[] { 1, 2, 3 },
                payload.GetProperty("Values").EnumerateArray().Select(static value => value.GetInt64()).ToArray());
        });
    }

    [TestMethod]
    public void Query_WhenScalarKindsConflict_ConsumesConcreteJsonElementsInRequestedOrder()
    {
        WithJson("[{\"Id\":1,\"Value\":1},{\"Id\":2,\"Value\":\"one\"},{\"Id\":3,\"Value\":true}]", path =>
        {
            var table = Compile($"select Id, JsonText(Value) as Value from json.file('{QueryPath(path)}') order by Id").Run();
            Assert.AreEqual(3, table.Count);
            CollectionAssert.AreEqual(new[] { "Id", "Value" }, table.Columns.Select(static column => column.ColumnName).ToArray());
            Assert.AreEqual(typeof(long), table.Columns.First().ColumnType);
            Assert.AreEqual(typeof(string), table.Columns.Last().ColumnType);
            CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, table.Select(static row => row.GetValue<long>(0)).ToArray());
            Assert.AreEqual("1", table[0].GetValue<string>(1));
            Assert.AreEqual("\"one\"", table[1].GetValue<string>(1));
            Assert.AreEqual("true", table[2].GetValue<string>(1));
        });
    }

    [TestMethod]
    public void Schema_WhenErasedRowTransferIsRequested_RejectsLegacyContract()
    {
        WithJson("{}", path =>
        {
            var context = RuntimeV2TestContexts.CreateExecutionContext();
            var exception = Assert.ThrowsExactly<NotSupportedException>(() =>
                NativeSourceCalls.Source(new JsonSchema(), Musoq.Schema.SourceArguments.Create(path, Musoq.Schema.SourceArguments.Empty))
                    .Read<object[]>("file", context));
            StringAssert.Contains(exception.Message, "query-scoped concrete rows");
        });
    }

    [TestMethod]
    [DataRow("Value > 1.5", 2L, 3L)]
    [DataRow("Value < 2.5", 1L, 2L)]
    public void Query_WhenIntegerColumnIsComparedWithFraction_PreservesNumericComparison(string predicate, long first, long second)
    {
        WithJson("[{\"Value\":1},{\"Value\":2},{\"Value\":3}]", path =>
        {
            using var compiled = Compile($"select Value from json.file('{QueryPath(path)}') where {predicate} order by Value");
            using var table = compiled.Run();
            Assert.AreEqual("Value", table.Columns.Single().ColumnName);
            Assert.AreEqual(typeof(long), table.Columns.Single().ColumnType);
            Assert.AreEqual(2, table.Count);
            CollectionAssert.AreEqual(new[] { first, second }, table.Select(static row => row.GetValue<long>(0)).ToArray());
        });
    }

    [TestMethod]
    public void Query_WhenTypedArrayHelpersAreCalled_ProducesScalarResults()
    {
        WithJson("{\"Values\":[1,2,3]}", path =>
        {
            using var compiled = Compile($"select Length(Values) as Size, MakeFlat(Values) as Flat from json.file('{QueryPath(path)}')");
            using var table = compiled.Run();
            Assert.AreEqual(1, table.Count);
            CollectionAssert.AreEqual(new[] { "Size", "Flat" }, table.Columns.Select(static column => column.ColumnName).ToArray());
            CollectionAssert.AreEqual(new[] { typeof(int?), typeof(string) }, table.Columns.Select(static column => column.ColumnType).ToArray());
            Assert.AreEqual(3, table[0].GetValue<int?>(0));
            Assert.AreEqual("1, 2, 3", table[0].GetValue<string>(1));
        });
    }

    [TestMethod]
    public void Query_WhenSeededNullableRowsAreFilteredAndPaged_MatchesIndependentExpectedRows()
    {
        var random = new Random(1729);
        var values = Enumerable.Range(0, 257).Select(index =>
            new { Id = (long)index, Value = index % 7 == 0 ? (long?)null : random.Next(-20, 21) }).ToArray();
        var expected = values.Where(static row => row.Value > -3.5m && row.Value < 7.5m)
            .OrderBy(static row => row.Id).Skip(3).Take(17).ToArray();
        WithJson(System.Text.Json.JsonSerializer.Serialize(values), path =>
        {
            using var compiled = Compile($"select Id, Value from json.file('{QueryPath(path)}') where Value > -3.5 and Value < 7.5 order by Id skip 3 take 17");
            using var table = compiled.Run();
            CollectionAssert.AreEqual(new[] { "Id", "Value" }, table.Columns.Select(static column => column.ColumnName).ToArray());
            CollectionAssert.AreEqual(new[] { typeof(long), typeof(long?) }, table.Columns.Select(static column => column.ColumnType).ToArray());
            Assert.AreEqual(17, table.Count);
            CollectionAssert.AreEqual(expected.Select(static row => row.Id).ToArray(), table.Select(static row => row.GetValue<long>(0)).ToArray());
            CollectionAssert.AreEqual(expected.Select(static row => row.Value).ToArray(), table.Select(static row => row.GetValue<long?>(1)).ToArray());
        });
    }

    [TestMethod]
    public void Query_WhenDeclaredIntegerConversionFeedsPredicate_PreservesConversionFailure()
    {
        WithJson("[{\"Value\":1.25},{\"Value\":2.5}]", path =>
        {
            using var compiled = Compile("table Contract { Value: long }; couple json.file with table Contract as Values; " +
                $"select Value from Values('{QueryPath(path)}') where Value > 100");
            var exception = Assert.Throws<Exception>(() =>
            {
                using var table = compiled.Run();
                foreach (var row in table)
                    _ = row.GetValue<long>(0);
            });
            Assert.IsTrue(HasConversionFailure(exception), "The declared integer conversion must fail before filtering can hide the invalid row.");
            StringAssert.Contains(FlattenMessages(exception), "Value");
        });
    }

    [TestMethod]
    public void Query_WhenFlattenedArrayStartsWithEmptyString_PreservesElementSeparators()
    {
        WithJson("{\"Values\":[\"\",\"x\",\"\"]}", path =>
        {
            using var compiled = Compile($"select MakeFlat(Values) as Flat from json.file('{QueryPath(path)}')");
            using var table = compiled.Run();
            Assert.AreEqual(1, table.Count);
            Assert.AreEqual("Flat", table.Columns.Single().ColumnName);
            Assert.AreEqual(typeof(string), table.Columns.Single().ColumnType);
            Assert.AreEqual(", x, ", table[0].GetValue<string>(0));
        });
    }

    [TestMethod]
    public void Query_WhenStructuredValuesAreNullOrMissing_PreservesNullableHelperResults()
    {
        WithJson("[{\"Id\":1,\"Values\":[1,2]},{\"Id\":2,\"Values\":null},{\"Id\":3}]", path =>
        {
            using var compiled = Compile($"select Id, JsonText(Values) as Json, Length(Values) as Size, MakeFlat(Values) as Flat from json.file('{QueryPath(path)}') order by Id");
            using var table = compiled.Run();
            Assert.AreEqual(3, table.Count);
            CollectionAssert.AreEqual(new[] { "Id", "Json", "Size", "Flat" }, table.Columns.Select(static column => column.ColumnName).ToArray());
            CollectionAssert.AreEqual(new[] { typeof(long), typeof(string), typeof(int?), typeof(string) }, table.Columns.Select(static column => column.ColumnType).ToArray());
            CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, table.Select(static row => row.GetValue<long>(0)).ToArray());
            CollectionAssert.AreEqual(new string[] { "[1,2]", null, null }, table.Select(static row => row.GetValue<string>(1)).ToArray());
            CollectionAssert.AreEqual(new int?[] { 2, null, null }, table.Select(static row => row.GetValue<int?>(2)).ToArray());
            CollectionAssert.AreEqual(new string[] { "1, 2", null, null }, table.Select(static row => row.GetValue<string>(3)).ToArray());
        });
    }

    private static bool HasConversionFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is FormatException)
                return true;
        return false;
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            $"JsonDynamic_{Guid.NewGuid():N}",
            new JsonSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static string FlattenMessages(Exception exception)
    {
        var messages = string.Empty;
        for (var current = exception; current is not null; current = current.InnerException)
            messages += current.Message + Environment.NewLine;
        return messages;
    }

    private static string QueryPath(string path)
    {
        return Path.GetFullPath(path)
            .Replace('\\', '/')
            .Replace("'", "''", StringComparison.Ordinal);
    }

    private static void WithJson(string json, Action<string> assertion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-json-query-{Guid.NewGuid():N}.json");
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
}

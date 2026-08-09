#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.DataSources.Structured;
using Musoq.Evaluator;
using Musoq.Schema;
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
            var table = Compile($"select Name, Age from #json.file('{QueryPath(path)}')").Run();

            Assert.AreEqual(1, table.Count);
            Assert.AreEqual("Ada", table[0][0]);
            Assert.AreEqual(36L, table[0][1]);
        });
    }

    [TestMethod]
    public void Query_WhenRowsAreSparse_ReturnsNullForMissingValues()
    {
        WithJson("[{\"Id\":1},{\"Id\":2,\"Late\":\"yes\"}]", path =>
        {
            var table = Compile($"select Id, Late from #json.file('{QueryPath(path)}') order by Id").Run();

            Assert.AreEqual(typeof(long), table.Columns.ElementAt(0).ColumnType);
            Assert.AreEqual(typeof(string), table.Columns.ElementAt(1).ColumnType);
            Assert.AreEqual(2, table.Count);
            Assert.IsNull(table[0][1]);
            Assert.AreEqual("yes", table[1][1]);
        });
    }

    [TestMethod]
    public void Query_WhenColumnDoesNotExist_FailsCompilation()
    {
        WithJson("{\"Name\":\"Ada\"}", path =>
        {
            var exception = Assert.ThrowsExactly<StructuredUnknownColumnException>(() =>
                Compile($"select Missing from #json.file('{QueryPath(path)}')"));

            StringAssert.Contains(FlattenMessages(exception), "Missing");
        });
    }

    [TestMethod]
    public void Query_WhenColumnCaseDoesNotMatch_FailsCompilation()
    {
        WithJson("{\"Name\":\"Ada\"}", path =>
        {
            var exception = Assert.ThrowsExactly<StructuredUnknownColumnException>(() =>
                Compile($"select name from #json.file('{QueryPath(path)}')"));

            StringAssert.Contains(FlattenMessages(exception), "name");
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
            var source = new JsonSource(path, context);

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
            new JsonSchema().GetRowSource<object[]>("file", context, stream));
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

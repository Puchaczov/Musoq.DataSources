#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Converter.Exceptions;
using Musoq.DataSources.Structured;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class SeparatedValuesDynamicSchemaTests
{
    [TestMethod]
    public void Query_WhenColumnDoesNotExist_FailsCompilation()
    {
        WithCsv("Name\nAda\n", path =>
        {
            var exception = Assert.ThrowsExactly<MusoqQueryException>(() =>
                Compile($"select Missing from #separatedvalues.comma('{QueryPath(path)}', true, 0)"));

            StringAssert.Contains(FlattenMessages(exception), "Missing");
        });
    }

    [TestMethod]
    public void Query_WhenColumnCaseDoesNotMatch_FailsCompilation()
    {
        WithCsv("Name\nAda\n", path =>
        {
            var exception = Assert.ThrowsExactly<MusoqQueryException>(() =>
                Compile($"select name from #separatedvalues.comma('{QueryPath(path)}', true, 0)"));

            StringAssert.Contains(FlattenMessages(exception), "name");
        });
    }

    [TestMethod]
    public void Planner_WhenProjectionIsExplicit_BindsDenseLayoutAndExactRowCount()
    {
        WithCsv("A,B,C\n1,2,3\n4,5,6\n", path =>
        {
            var request = new SourcePlanRequest
            {
                Identity = SourceIdentity.Empty,
                RequiredColumns = [new SourceColumnRef("C")],
                SourceRuntimeSettings = new Dictionary<string, string>(),
                Predicate = null,
                OrderBy = [],
                Skip = null,
                Take = null
            };

            var result = new SeparatedValuesSchema().TryPlanSource("comma", request, path, true, 0);
            var layout = (StructuredExecutionLayout)result.ExecutionPlan.Properties![
                SeparatedValuesPlanning.LayoutPropertyName]!;

            Assert.AreEqual(1, layout.Bindings.Length);
            Assert.AreEqual("C", layout.Bindings[0].Name);
            Assert.AreEqual(2, layout.Bindings[0].SourceOrdinal);
            Assert.AreEqual(0, layout.Bindings[0].OutputOrdinal);
        });
    }

    [TestMethod]
    public void Source_WhenBoundTypeChangesBeforeExecution_FailsWithSchemaDrift()
    {
        WithCsv("Value\n1\n", path =>
        {
            var request = new SourcePlanRequest
            {
                Identity = SourceIdentity.Empty,
                RequiredColumns = [new SourceColumnRef("Value")],
                SourceRuntimeSettings = new Dictionary<string, string>(),
                Predicate = null,
                OrderBy = [],
                Skip = null,
                Take = null
            };
            var plan = new SeparatedValuesSchema()
                .TryPlanSource("comma", request, path, true, 0)
                .ExecutionPlan;
            File.WriteAllText(path, "Value\nchanged\n", new UTF8Encoding(false, true));
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Value", 0, typeof(long))],
                executionPlan: plan);
            var source = new SeparatedValuesFromFileRowsSource(path, ",", true, 0, context);

            var exception = Assert.ThrowsExactly<StructuredSchemaDriftException>(() =>
                source.Chunks.SelectMany(chunk => chunk).ToArray());

            StringAssert.Contains(FlattenMessages(exception), "changed incompatibly after binding");
        });
    }

    [TestMethod]
    public void Schema_WhenStreamParameterIsUsed_RejectsIt()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Name\nAda\n"));
        var context = RuntimeV2TestContexts.CreateExecutionContext();

        Assert.ThrowsExactly<ArgumentException>(() =>
            new SeparatedValuesSchema().GetRowSource<object[]>("comma", context, stream, true, 0));
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            $"SeparatedValuesDynamic_{Guid.NewGuid():N}",
            new CsvSchemaProvider(),
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

    private static void WithCsv(string contents, Action<string> assertion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-csv-query-{Guid.NewGuid():N}.csv");
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

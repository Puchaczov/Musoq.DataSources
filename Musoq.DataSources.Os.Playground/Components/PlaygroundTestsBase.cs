using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Os;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Playground.Components;

public abstract class PlaygroundTestsBase
{
    static PlaygroundTestsBase()
    {
        Culture.ApplyWithDefaultCulture();
    }

    protected static void RunProbe(TestContext testContext, string query, params string[] expectedColumns)
    {
        testContext.WriteLine($"Query: {query}");

        try
        {
            using var compiledQuery = InstanceCreatorHelpers.CompileForExecution(
                query,
                Guid.NewGuid().ToString(),
                new PlaygroundOsSchemaProvider(),
                EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());

            var table = compiledQuery.Run();
            var columns = table.Columns.ToArray();

            testContext.WriteLine(
                $"Columns: {string.Join(", ", columns.Select(column => $"{column.ColumnName} ({column.ColumnType.Name})"))}");
            testContext.WriteLine($"Rows: {table.Count}");

            foreach (var expectedColumn in expectedColumns)
            {
                Assert.IsTrue(
                    columns.Any(column => string.Equals(
                        column.ColumnName,
                        expectedColumn,
                        StringComparison.OrdinalIgnoreCase)),
                    $"Expected column '{expectedColumn}' was not returned.");
            }

            var sampleCount = Math.Min(3, table.Count);
            for (var index = 0; index < sampleCount; index++)
            {
                var values = table[index].Values
                    .Select(value => value?.ToString() ?? "<null>");

                testContext.WriteLine($"Row {index}: {string.Join(" | ", values)}");
            }
        }
        catch (Exception exception)
        {
            testContext.WriteLine($"ERROR: {exception}");
            Assert.Fail(exception.ToString());
        }
    }

    private sealed class PlaygroundOsSchemaProvider : ISchemaProvider
    {
        public ISchema GetSchema(string schema) => new OsSchema();
    }
}

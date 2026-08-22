#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class SeparatedValuesProgressReportingTests
{
    [TestMethod]
    public void FileRowsSource_ReportsRowsReadAndEmittedRows()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(tempFile, "Name,Age\r\nAlice,31\r\nBob,42\r\nCarol,53\r\n", Encoding.UTF8);

        try
        {
            var capture = new DataSourceProgressCapture();
            var request = CreateRequest(
                GreaterThan("Age", 40),
                [new SourceColumnRef("Name")],
                skip: null,
                take: null);
            var plan = new SeparatedValuesSchema()
                .TryPlanSource("comma", request, tempFile, true, 0)
                .ExecutionPlan;
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Name", 0, typeof(string))],
                dataSourceProgressCallback: capture.Handler,
                executionPlan: plan);
            var source = SeparatedValuesNativeTestSource.Create<string>(tempFile, ",", true, 0, context);

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual(1, capture.For("separated_values", DataSourcePhase.Begin).Count);
            Assert.AreEqual(3, capture.For("separated_values", DataSourcePhase.RowsRead).Single().RowsProcessed);
            Assert.AreEqual(2, capture.For("separated_values", DataSourcePhase.End).Single().RowsProcessed);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [TestMethod]
    public void FileRowsSource_WhenAcceptedTakeIsZero_ReportsEndWithoutRowsRead()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(tempFile, "Name,Age\r\nAlice,31\r\n", Encoding.UTF8);

        try
        {
            var capture = new DataSourceProgressCapture();
            var request = CreateRequest(
                null,
                [new SourceColumnRef("Name")],
                skip: null,
                take: 0);
            var plan = new SeparatedValuesSchema()
                .TryPlanSource("comma", request, tempFile, true, 0)
                .ExecutionPlan;
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                CancellationToken.None,
                [new SchemaColumn("Name", 0, typeof(string))],
                dataSourceProgressCallback: capture.Handler,
                executionPlan: plan);
            var source = SeparatedValuesNativeTestSource.Create<string>(tempFile, ",", true, 0, context);

            var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

            Assert.AreEqual(0, rows.Length);
            Assert.AreEqual(1, capture.For("separated_values", DataSourcePhase.Begin).Count);
            Assert.AreEqual(0, capture.For("separated_values", DataSourcePhase.RowsRead).Count);
            Assert.AreEqual(0, capture.For("separated_values", DataSourcePhase.End).Single().RowsProcessed);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    private static SourcePlanRequest CreateRequest(
        SourcePredicateExpression? predicate,
        IReadOnlyList<SourceColumnRef> requiredColumns,
        long? skip,
        long? take)
    {
        return new SourcePlanRequest
        {
            Identity = CreateIdentity(),
            RequiredColumns = requiredColumns,
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = [],
            Skip = skip,
            Take = take
        };
    }

    private static SourceIdentity CreateIdentity()
    {
        return new SourceIdentity("separatedvalues", "separatedvalues", "separatedvalues", "comma");
    }

    private static SourcePredicateComparison GreaterThan(string columnName, object value)
    {
        return new SourcePredicateComparison(
            SourcePredicateComparisonOperator.GreaterThan,
            new SourcePredicateColumn(new SourceColumnRef(columnName)),
            new SourcePredicateLiteral(value));
    }
}

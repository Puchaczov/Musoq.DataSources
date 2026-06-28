using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.CANBus.Messages;
using Musoq.DataSources.CANBus.Signals;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.CANBus.Tests;

[TestClass]
public class CANBusRuntimeV2ProjectionTests
{
    [TestMethod]
    public void TryPlanSource_WhenSeparatedValuesDynamicRequiredColumnsAndFramePredicateArePresent_AcceptsProjectionAndPredicate()
    {
        var schema = new CANBusSchema();
        var predicate = Equal("ID", 292u);
        var request = CreateRequest(predicate, [new SourceColumnRef("Engine")]);

        var result = schema.TryPlanSource("separatedvalues", request, "frames.csv", "frames.dbc");

        Assert.AreEqual(1, result.AcceptedColumns.Count);
        Assert.AreEqual("Engine", result.AcceptedColumns[0].Name);
        Assert.AreEqual(1, result.ExecutionPlan.AcceptedColumns.Count);
        Assert.AreSame(predicate, result.AcceptedPredicate);
        Assert.AreSame(predicate, result.ExecutionPlan.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(request.Skip, result.ResidualSkip);
        Assert.AreEqual(request.Take, result.ResidualTake);
    }

    [TestMethod]
    public void TryPlanSource_WhenSeparatedValuesBaseColumnsOnlyArePresent_DoesNotAcceptProjection()
    {
        var schema = new CANBusSchema();
        var request = CreateRequest(null, [new SourceColumnRef("ID")]);

        var result = schema.TryPlanSource("separatedvalues", request, "frames.csv", "frames.dbc");

        Assert.AreEqual(0, result.AcceptedColumns.Count);
        Assert.AreEqual(0, result.ExecutionPlan.AcceptedColumns.Count);
    }

    [TestMethod]
    public void TryPlanSource_WhenTypedCanTablesRequiredColumnsArePresent_DoesNotAcceptProjection()
    {
        var schema = new CANBusSchema();
        var request = CreateRequest(null, [new SourceColumnRef("Name")]);

        var result = schema.TryPlanSource("messages", request, "frames.dbc");

        Assert.AreEqual(0, result.AcceptedColumns.Count);
        Assert.AreEqual(0, result.ExecutionPlan.AcceptedColumns.Count);
    }

    [TestMethod]
    public void TryPlanSource_WhenSeparatedValuesDlcPredicateIsRequested_AcceptsPredicate()
    {
        var schema = new CANBusSchema();
        var predicate = Equal("DLC", (byte)8);
        var request = CreateRequest(predicate, []);

        var result = schema.TryPlanSource("separatedvalues", request, "frames.csv", "frames.dbc");

        Assert.AreSame(predicate, result.AcceptedPredicate);
        Assert.AreSame(predicate, result.ExecutionPlan.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
    }

    [TestMethod]
    public void TryPlanSource_WhenSeparatedValuesDynamicSignalPredicateIsRequested_LeavesPredicateResidual()
    {
        var schema = new CANBusSchema();
        var predicate = Equal("Engine.Oil_Temperature", 124d);
        var request = CreateRequest(predicate, [new SourceColumnRef("Engine")]);

        var result = schema.TryPlanSource("separatedvalues", request, "frames.csv", "frames.dbc");

        Assert.IsNull(result.AcceptedPredicate);
        Assert.AreSame(predicate, result.ResidualPredicate);
    }

    [TestMethod]
    public void TryPlanSource_WhenMessagesIdentityPredicateIsRequested_AcceptsPredicateOnly()
    {
        var schema = new CANBusSchema();
        var predicate = Equal("Name", "Engine");
        var request = CreateRequest(predicate, []);

        var result = schema.TryPlanSource("messages", request, "frames.dbc");

        Assert.AreSame(predicate, result.AcceptedPredicate);
        Assert.AreSame(predicate, result.ExecutionPlan.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(0, result.AcceptedColumns.Count);
        Assert.AreEqual(0, result.ExecutionPlan.AcceptedColumns.Count);
        Assert.AreEqual(request.Skip, result.ResidualSkip);
        Assert.AreEqual(request.Take, result.ResidualTake);
    }

    [TestMethod]
    public void TryPlanSource_WhenSignalsIdentityPredicateIsRequested_AcceptsPredicateOnly()
    {
        var schema = new CANBusSchema();
        var predicate = Equal("MessageName", "Engine");
        var request = CreateRequest(predicate, []);

        var result = schema.TryPlanSource("signals", request, "frames.dbc");

        Assert.AreSame(predicate, result.AcceptedPredicate);
        Assert.AreSame(predicate, result.ExecutionPlan.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(0, result.AcceptedColumns.Count);
        Assert.AreEqual(0, result.ExecutionPlan.AcceptedColumns.Count);
        Assert.AreEqual(request.Skip, result.ResidualSkip);
        Assert.AreEqual(request.Take, result.ResidualTake);
    }

    [TestMethod]
    public void MessageFrameObjectRows_WhenProjectionIsAccepted_OutputsOnlyAcceptedColumns()
    {
        var columns = new ISchemaColumn[]
        {
            new SchemaColumn("ID", 0, typeof(uint)),
            new SchemaColumn("Data", 4, typeof(ulong)),
            new SchemaColumn("Engine", 7, typeof(object))
        };
        var executionContext = RuntimeV2TestContexts.CreateExecutionContext(
            CancellationToken.None,
            columns,
            executionPlan: CreateExecutionPlan([new SourceColumnRef("ID")]));
        var schema = new CANBusSchema();
        var source = schema.GetRowSource<object>(
            "separatedvalues",
            executionContext,
            "./Data/1/1.csv",
            "./Data/1/1.dbc",
            "dec",
            "big");

        var rows = source.Chunks.SelectMany(chunk => chunk).Cast<Dictionary<string, object?>>().ToArray();

        Assert.AreEqual(4, rows.Length);
        Assert.IsTrue(rows.All(row => row.Count == 1));
        Assert.IsTrue(rows.All(row => row.ContainsKey("ID")));
        Assert.IsTrue(rows.Any(row => (uint)row["ID"]! == 292u));
        Assert.IsTrue(rows.All(row => !row.ContainsKey("Data")));
        Assert.IsTrue(rows.All(row => !row.ContainsKey("Engine")));
    }

    [TestMethod]
    public void MessageFrameObjectRows_WhenPredicateIsAccepted_EmitsFilteredRows()
    {
        var columns = new ISchemaColumn[]
        {
            new SchemaColumn("ID", 0, typeof(uint)),
            new SchemaColumn("Data", 4, typeof(ulong)),
            new SchemaColumn("Engine", 7, typeof(object))
        };
        var predicate = Equal("ID", 292u);
        var executionContext = RuntimeV2TestContexts.CreateExecutionContext(
            CancellationToken.None,
            columns,
            executionPlan: CreateExecutionPlan([], predicate));
        var schema = new CANBusSchema();
        var source = schema.GetRowSource<object>(
            "separatedvalues",
            executionContext,
            "./Data/1/1.csv",
            "./Data/1/1.dbc",
            "dec",
            "big");

        var rows = source.Chunks.SelectMany(chunk => chunk).Cast<Dictionary<string, object?>>().ToArray();

        Assert.AreEqual(2, rows.Length);
        Assert.IsTrue(rows.All(row => (uint)row["ID"]! == 292u));
    }

    [TestMethod]
    public void MessagesRows_WhenPredicateIsAccepted_EmitsFilteredRows()
    {
        var predicate = Equal("Name", "Engine");
        var executionContext = RuntimeV2TestContexts.CreateExecutionContext(
            executionPlan: CreateExecutionPlan([], predicate));
        var source = new CANBusSchema().GetRowSource<MessageEntity>(
            "messages",
            executionContext,
            "./Data/1/1.dbc");

        var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

        Assert.AreEqual(1, rows.Length);
        Assert.AreEqual("Engine", rows[0].Name);
    }

    [TestMethod]
    public void SignalsRows_WhenPredicateIsAccepted_EmitsFilteredRows()
    {
        var predicate = Equal("MessageName", "Engine");
        var executionContext = RuntimeV2TestContexts.CreateExecutionContext(
            executionPlan: CreateExecutionPlan([], predicate));
        var source = new CANBusSchema().GetRowSource<SignalEntity>(
            "signals",
            executionContext,
            "./Data/1/1.dbc");

        var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

        Assert.AreEqual(2, rows.Length);
        Assert.IsTrue(rows.All(row => row.MessageName == "Engine"));
    }

    private static SourcePlanRequest CreateRequest(
        SourcePredicateExpression? predicate,
        IReadOnlyList<SourceColumnRef> requiredColumns)
    {
        return new SourcePlanRequest
        {
            Identity = new SourceIdentity("can", "can", "can", "separatedvalues"),
            RequiredColumns = requiredColumns,
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = [],
            Skip = 1,
            Take = 2
        };
    }

    private static SourceExecutionPlan CreateExecutionPlan(
        IReadOnlyList<SourceColumnRef> acceptedColumns,
        SourcePredicateExpression? acceptedPredicate = null)
    {
        return new SourceExecutionPlan
        {
            Identity = new SourceIdentity("can", "can", "can", "separatedvalues"),
            AcceptedColumns = acceptedColumns,
            AcceptedPredicate = acceptedPredicate,
            AcceptedOrderBy = []
        };
    }

    private static SourcePredicateComparison Equal(string columnName, object value)
    {
        return new SourcePredicateComparison(
            SourcePredicateComparisonOperator.Equal,
            new SourcePredicateColumn(new SourceColumnRef(columnName)),
            new SourcePredicateLiteral(value));
    }
}

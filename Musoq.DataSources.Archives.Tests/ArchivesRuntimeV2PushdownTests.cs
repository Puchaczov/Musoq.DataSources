#nullable enable

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Archives.Tests;

[TestClass]
public class ArchivesRuntimeV2PushdownTests
{
    [TestMethod]
    public void TryPlanSource_WhenSupportedPredicatesAreRequested_AcceptsOnlyPredicate()
    {
        var predicate = And(Equal("a.IsDirectory", false), GreaterThan("Size", 0L));
        var request = CreateRequest(
            predicate,
            [new OrderByExpression(new SourceColumnRef("Key"), OrderDirection.Ascending)],
            skip: 1,
            take: 2);

        var result = new ArchivesSchema().TryPlanSource("file", request, "./Files/Example1/archives.zip");

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(0, result.AcceptedOrderBy.Count);
        Assert.AreEqual(1, result.ResidualOrderBy.Count);
        Assert.IsNull(result.AcceptedSkip);
        Assert.AreEqual(1, result.ResidualSkip);
        Assert.IsNull(result.AcceptedTake);
        Assert.AreEqual(2, result.ResidualTake);
        AssertPlanMatchesExecution(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenUnsupportedPredicateIsRequested_LeavesPredicateResidual()
    {
        var request = CreateRequest(Equal("CompressedSize", 1L), [], skip: null, take: null);

        var result = new ArchivesSchema().TryPlanSource("file", request, "./Files/Example1/archives.zip");

        Assert.IsNull(result.AcceptedPredicate);
        Assert.AreSame(request.Predicate, result.ResidualPredicate);
        AssertPlanMatchesExecution(result);
    }

    [TestMethod]
    public void ArchivesRowSource_WhenExecutionPlanHasPredicate_EmitsFilteredRows()
    {
        var predicate = Equal("Key", "text1.txt");
        var executionPlan = new SourceExecutionPlan
        {
            Identity = CreateIdentity(),
            AcceptedPredicate = predicate,
            AcceptedOrderBy = []
        };
        var source = new ArchivesSchema().GetRowSource<EntryWrapper>(
            "file",
            RuntimeV2TestContexts.CreateExecutionContext(executionPlan: executionPlan),
            "./Files/Example1/archives.zip");

        var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

        Assert.AreEqual(1, rows.Length);
        Assert.AreEqual("text1.txt", rows[0].Key);
        Assert.IsFalse(rows[0].IsDirectory);
    }

    private static SourcePlanRequest CreateRequest(
        SourcePredicateExpression? predicate,
        IReadOnlyList<OrderByExpression> orderBy,
        long? skip,
        long? take)
    {
        return new SourcePlanRequest
        {
            Identity = CreateIdentity(),
            RequiredColumns = [],
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = orderBy,
            Skip = skip,
            Take = take
        };
    }

    private static void AssertPlanMatchesExecution(SourcePlanResult result)
    {
        Assert.AreSame(result.AcceptedPredicate, result.ExecutionPlan.AcceptedPredicate);
        Assert.AreEqual(0, result.ExecutionPlan.AcceptedOrderBy.Count);
        Assert.IsNull(result.ExecutionPlan.AcceptedSkip);
        Assert.IsNull(result.ExecutionPlan.AcceptedTake);
        Assert.AreEqual(0, result.AcceptedColumns.Count);
        Assert.AreEqual(0, result.ExecutionPlan.AcceptedColumns.Count);
    }

    private static SourceIdentity CreateIdentity()
    {
        return new SourceIdentity("archives", "archives", "archives", "file");
    }

    private static SourcePredicateLogical And(SourcePredicateExpression left, SourcePredicateExpression right)
    {
        return new SourcePredicateLogical(SourcePredicateLogicalOperator.And, left, right);
    }

    private static SourcePredicateComparison Equal(string columnName, object value)
    {
        return Compare(SourcePredicateComparisonOperator.Equal, columnName, value);
    }

    private static SourcePredicateComparison GreaterThan(string columnName, object value)
    {
        return Compare(SourcePredicateComparisonOperator.GreaterThan, columnName, value);
    }

    private static SourcePredicateComparison Compare(
        SourcePredicateComparisonOperator op,
        string columnName,
        object value)
    {
        return new SourcePredicateComparison(
            op,
            new SourcePredicateColumn(new SourceColumnRef(columnName)),
            new SourcePredicateLiteral(value));
    }
}

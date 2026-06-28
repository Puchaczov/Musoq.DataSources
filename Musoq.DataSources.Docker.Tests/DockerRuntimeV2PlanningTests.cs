#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Docker.DotNet.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Docker.Containers;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Docker.Tests;

[TestClass]
public class DockerRuntimeV2PlanningTests
{
    [TestMethod]
    public void TryPlanSource_WhenContainerScalarPredicateIsRequested_AcceptsPredicateOnly()
    {
        var predicate = Equal("Status", "running");
        var request = CreateRequest("containers", predicate, skip: 1, take: 2);

        var result = new DockerSchema().TryPlanSource("containers", request);

        Assert.AreSame(predicate, result.AcceptedPredicate);
        Assert.AreSame(predicate, result.ExecutionPlan.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(0, result.AcceptedOrderBy.Count);
        Assert.AreEqual(0, result.ExecutionPlan.AcceptedOrderBy.Count);
        Assert.IsNull(result.AcceptedSkip);
        Assert.IsNull(result.AcceptedTake);
        Assert.AreEqual(1, result.ResidualSkip);
        Assert.AreEqual(2, result.ResidualTake);
        AssertNoProjectionAccepted(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenUnsupportedContainerPredicateIsRequested_LeavesPredicateResidual()
    {
        var predicate = Equal("Names", "api");
        var request = CreateRequest("containers", predicate, skip: null, take: null);

        var result = new DockerSchema().TryPlanSource("containers", request);

        Assert.IsNull(result.AcceptedPredicate);
        Assert.AreSame(predicate, result.ResidualPredicate);
        AssertNoProjectionAccepted(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenScalarPredicatesAreRequestedForOtherTables_AcceptsPredicateOnly()
    {
        AssertPredicateAccepted("images", GreaterThan("Size", 1024L));
        AssertPredicateAccepted("networks", Equal("Internal", true));
        AssertPredicateAccepted("volumes", Equal("Name", "cache"));
    }

    [TestMethod]
    public void ContainersSource_WhenExecutionPlanHasPredicate_EmitsFilteredRows()
    {
        var api = new Mock<IDockerApi>();
        api.Setup(f => f.ListContainersAsync())
            .ReturnsAsync(new List<ContainerListResponse>
            {
                new() { ID = "1", Status = "running" },
                new() { ID = "2", Status = "exited" }
            });
        var executionPlan = new SourceExecutionPlan
        {
            Identity = CreateIdentity("containers"),
            AcceptedPredicate = Equal("Status", "running"),
            AcceptedOrderBy = []
        };
        var source = new ContainersSource(
            api.Object,
            RuntimeV2TestContexts.CreateExecutionContext(executionPlan: executionPlan));

        var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

        Assert.AreEqual(1, rows.Length);
        Assert.AreEqual("1", rows[0].ID);
        api.Verify(f => f.ListContainersAsync(), Times.Once);
    }

    private static void AssertPredicateAccepted(string tableName, SourcePredicateExpression predicate)
    {
        var request = CreateRequest(tableName, predicate, skip: null, take: null);

        var result = new DockerSchema().TryPlanSource(tableName, request);

        Assert.AreSame(predicate, result.AcceptedPredicate);
        Assert.AreSame(predicate, result.ExecutionPlan.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        AssertNoProjectionAccepted(result);
    }

    private static SourcePlanRequest CreateRequest(
        string tableName,
        SourcePredicateExpression? predicate,
        long? skip,
        long? take)
    {
        return new SourcePlanRequest
        {
            Identity = CreateIdentity(tableName),
            RequiredColumns = [],
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = [],
            Skip = skip,
            Take = take
        };
    }

    private static SourceIdentity CreateIdentity(string tableName)
    {
        return new SourceIdentity("docker", "docker", "docker", tableName);
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

    private static void AssertNoProjectionAccepted(SourcePlanResult result)
    {
        Assert.AreEqual(0, result.AcceptedColumns.Count);
        Assert.AreEqual(0, result.ExecutionPlan.AcceptedColumns.Count);
    }
}

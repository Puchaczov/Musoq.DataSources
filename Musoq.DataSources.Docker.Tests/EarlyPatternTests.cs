using System;
using System.Linq;
using Docker.DotNet.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Docker.Containers;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.Diagnostics;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Docker.Tests;

[TestClass]
public class EarlyPatternTests
{
    [TestMethod]
    public void RawContainerFiltering_AvoidsRejectedWrappers_AndKeepsPagingAfterFiltering()
    {
        var api = new Mock<IDockerApi>(MockBehavior.Strict);
        api.Setup(value => value.ListContainersAsync()).ReturnsAsync(Enumerable.Range(0, 100).Select(i => new ContainerListResponse { ID = i.ToString("D2"), Image = i % 2 == 0 ? "keep" : "reject" }).ToArray());
        var schema = new DockerSchema(api.Object);
        var predicate = new SourcePredicateRegexMatch(new("Image"), "^keep$", TimeSpan.FromMilliseconds(31));
        var plan = schema.TryPlanSource("containers", new() { Identity = new("docker", "s", "s", "containers"), Predicate = predicate, Skip = 1, Take = 1 });
        var metrics = new PatternWorkMetrics();
        var context = RuntimeV2TestContexts.CreateExecutionContext(executionPlan: plan.ExecutionPlan, sourceDiagnostics: new SourceDiagnostics(metrics));
        var row = schema.GetRowSource<ContainerEntity, EmptySourceArguments>("containers", context, SourceArguments.Empty).Chunks.SelectMany(chunk => chunk).Single();
        Assert.AreEqual("02", row.ID);
        Assert.AreEqual(100L, metrics["Pattern.Candidates"]);
        Assert.AreEqual(50L, metrics["Pattern.ConstructedRows"]);
        api.Verify(value => value.ListContainersAsync(), Times.Once);
    }
}

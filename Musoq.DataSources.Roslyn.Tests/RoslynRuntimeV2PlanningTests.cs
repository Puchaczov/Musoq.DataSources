using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public class RoslynRuntimeV2PlanningTests
{
    [TestMethod]
    public void TryPlanSource_WhenProjectAssemblyNamePredicateIsUsed_AcceptsPredicate()
    {
        var schema = new CSharpSchema();
        var request = CreateRequest(Equal("p.AssemblyName", "Solution1.ClassLibrary1"));

        var result = schema.TryPlanSource("solution", request, "solution.sln");

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.IsTrue(result.ExecutionPlan.Properties.ContainsKey("RoslynFilters"));
    }

    [TestMethod]
    public void TryPlanSource_WhenProjectNamePredicateUsesProjectAlias_AcceptsPredicate()
    {
        var schema = new CSharpSchema();
        var request = CreateRequest(Equal("p.Name", "Solution1.ClassLibrary1"));

        var result = schema.TryPlanSource("solution", request, "solution.sln");

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
    }

    [TestMethod]
    public void TryPlanSource_WhenClassNamePredicateUsesDifferentAlias_KeepsPredicateResidual()
    {
        var schema = new CSharpSchema();
        var request = CreateRequest(Equal("c.Name", "Class1"));

        var result = schema.TryPlanSource("solution", request, "solution.sln");

        Assert.IsNull(result.AcceptedPredicate);
        Assert.IsNotNull(result.ResidualPredicate);
    }

    [TestMethod]
    public void TryPlanSource_WhenPredicateHasUnsupportedSide_KeepsResidualPredicate()
    {
        var schema = new CSharpSchema();
        var request = CreateRequest(new SourcePredicateLogical(
            SourcePredicateLogicalOperator.And,
            Equal("p.AssemblyName", "Solution1.ClassLibrary1"),
            Equal("c.Name", "Class1")));

        var result = schema.TryPlanSource("solution", request, "solution.sln");

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNotNull(result.ResidualPredicate);
    }

    private static SourcePlanRequest CreateRequest(SourcePredicateExpression predicate)
    {
        return new SourcePlanRequest
        {
            Identity = new SourceIdentity("csharp", "csharp", "csharp", "csharp"),
            RequiredColumns = [],
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = []
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

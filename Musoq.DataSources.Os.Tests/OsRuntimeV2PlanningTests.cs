using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Tests;

[TestClass]
public class OsRuntimeV2PlanningTests
{
    [TestMethod]
    public void TryPlanSource_WhenFileExtensionPredicateIsUsed_AcceptsPredicate()
    {
        var schema = new OsSchema();
        var request = CreateRequest(Equal("f.Extension", ".txt"));

        var result = schema.TryPlanSource("files", request, ".", false);

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.IsTrue(result.ExecutionPlan.Properties.ContainsKey("OsFileFilters"));
    }

    [TestMethod]
    public void TryPlanSource_WhenFileNameWildcardLiteralIsUsed_KeepsPredicateResidual()
    {
        var schema = new OsSchema();
        var request = CreateRequest(Equal("f.Name", "*.txt"));

        var result = schema.TryPlanSource("files", request, ".", false);

        Assert.IsNull(result.AcceptedPredicate);
        Assert.IsNotNull(result.ResidualPredicate);
    }

    [TestMethod]
    public void TryPlanSource_WhenDirectoryNamePredicateIsUsed_AcceptsPredicate()
    {
        var schema = new OsSchema();
        var request = CreateRequest(Equal("d.Name", "Directory1"));

        var result = schema.TryPlanSource("directories", request, ".", false);

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.IsTrue(result.ExecutionPlan.Properties.ContainsKey("OsDirectoryFilters"));
    }

    private static SourcePlanRequest CreateRequest(SourcePredicateExpression predicate)
    {
        return new SourcePlanRequest
        {
            Identity = new SourceIdentity("os", "os", "os", "os"),
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

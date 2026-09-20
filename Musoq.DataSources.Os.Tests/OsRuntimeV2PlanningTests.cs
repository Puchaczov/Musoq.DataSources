using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Tests;

[TestClass]
public class OsRuntimeV2PlanningTests
{
    [DataTestMethod]
    [DataRow("file", "f.Name", false, false)]
    [DataRow("file", "f.FileName", false, false)]
    [DataRow("file", "f.Extension", false, false)]
    [DataRow("files", "f.Name", true, false)]
    [DataRow("files", "f.FileName", true, false)]
    [DataRow("files", "f.Extension", true, false)]
    [DataRow("dlls", "d.FileInfo.Name", false, false)]
    [DataRow("dlls", "d.FileInfo.Extension", false, false)]
    [DataRow("directories", "d.Name", false, true)]
    public void TryPlanSource_WhenSupportedLiteralEqualityIsUsed_AcceptsAccordingToSourceCapability(
        string sourceName,
        string columnName,
        bool emitsFileFilters,
        bool emitsDirectoryFilters)
    {
        var schema = new OsSchema();
        var result = schema.TryPlanSource(sourceName, CreateRequest(Equal(columnName, "value")), ".", false);

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(
            emitsFileFilters,
            result.ExecutionPlan.Properties.ContainsKey(OsSourcePlanner.FileFiltersPropertyName));
        Assert.AreEqual(
            emitsDirectoryFilters,
            result.ExecutionPlan.Properties.ContainsKey(OsSourcePlanner.DirectoryFiltersPropertyName));
    }

    [DataTestMethod]
    [DataRow("m.Name")]
    [DataRow("m.FileName")]
    [DataRow("m.Extension")]
    public void TryPlanSource_WhenMetadataLooksLikeAFilePredicate_KeepsItResidual(string columnName)
    {
        var schema = new OsSchema();
        var result = schema.TryPlanSource("metadata", CreateRequest(Equal(columnName, "value")), ".", false);

        Assert.IsNull(result.AcceptedPredicate);
        Assert.IsNotNull(result.ResidualPredicate);
        Assert.IsFalse(result.ExecutionPlan.Properties.ContainsKey(OsSourcePlanner.FileFiltersPropertyName));
    }

    [TestMethod]
    public void TryPlanSource_WhenSupportedAndUnsupportedPredicatesAreCombined_SplitsThemByAnd()
    {
        var schema = new OsSchema();
        var request = CreateRequest(new SourcePredicateLogical(
            SourcePredicateLogicalOperator.And,
            Equal("f.Extension", ".txt"),
            Compare(SourcePredicateComparisonOperator.GreaterThan, "f.Name", "File")));

        var result = schema.TryPlanSource("files", request, ".", false);

        Assert.IsInstanceOfType<SourcePredicateComparison>(result.AcceptedPredicate);
        Assert.IsInstanceOfType<SourcePredicateComparison>(result.ResidualPredicate);
        Assert.AreEqual(".txt", ((SourcePredicateLiteral)((SourcePredicateComparison)result.AcceptedPredicate!).Right).Value);
        Assert.AreEqual("File", ((SourcePredicateLiteral)((SourcePredicateComparison)result.ResidualPredicate!).Right).Value);
    }

    [TestMethod]
    public void TryPlanSource_WhenOrPredicateIsUsed_KeepsWholePredicateResidual()
    {
        var schema = new OsSchema();
        var predicate = new SourcePredicateLogical(
            SourcePredicateLogicalOperator.Or,
            Equal("f.Extension", ".txt"),
            Equal("f.Extension", ".log"));

        var result = schema.TryPlanSource("files", CreateRequest(predicate), ".", false);

        Assert.IsNull(result.AcceptedPredicate);
        Assert.AreSame(predicate, result.ResidualPredicate);
    }

    [DataTestMethod]
    [DataRow(SourcePredicateComparisonOperator.NotEqual, ".txt")]
    [DataRow(SourcePredicateComparisonOperator.Equal, "*.txt")]
    [DataRow(SourcePredicateComparisonOperator.Equal, "file?.txt")]
    public void TryPlanSource_WhenPredicateIsNotAnExactStringEquality_KeepsItResidual(
        SourcePredicateComparisonOperator op,
        string value)
    {
        var schema = new OsSchema();
        var result = schema.TryPlanSource("files", CreateRequest(Compare(op, "f.Extension", value)), ".", false);

        Assert.IsNull(result.AcceptedPredicate);
        Assert.IsNotNull(result.ResidualPredicate);
    }

    [TestMethod]
    public void TryPlanSource_WhenLiteralIsNotAString_KeepsPredicateResidual()
    {
        var schema = new OsSchema();
        var result = schema.TryPlanSource("files", CreateRequest(Equal("f.Extension", 42)), ".", false);

        Assert.IsNull(result.AcceptedPredicate);
        Assert.IsNotNull(result.ResidualPredicate);
    }

    [TestMethod]
    public void TryPlanSource_WhenColumnIsUnsupported_KeepsPredicateResidual()
    {
        var schema = new OsSchema();
        var result = schema.TryPlanSource("files", CreateRequest(Equal("f.Length", 42)), ".", false);

        Assert.IsNull(result.AcceptedPredicate);
        Assert.IsNotNull(result.ResidualPredicate);
    }

    [TestMethod]
    public void TryPlanSource_WhenFileExtensionPredicateIsUsed_AcceptsPredicate()
    {
        var schema = new OsSchema();
        var request = CreateRequest(Equal("f.Extension", ".txt"));

        var result = schema.TryPlanSource("files", request, ".", false);

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(result.AcceptedPredicate, result.ExecutionPlan.AcceptedPredicate);
        AssertNoProjectionAccepted(result);
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
        Assert.AreEqual(result.AcceptedPredicate, result.ExecutionPlan.AcceptedPredicate);
        AssertNoProjectionAccepted(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenFileExtensionWildcardLiteralIsUsed_KeepsPredicateResidual()
    {
        var schema = new OsSchema();
        var request = CreateRequest(Equal("f.Extension", "*.txt"));

        var result = schema.TryPlanSource("files", request, ".", false);

        Assert.IsNull(result.AcceptedPredicate);
        Assert.IsNotNull(result.ResidualPredicate);
        Assert.AreEqual(result.AcceptedPredicate, result.ExecutionPlan.AcceptedPredicate);
        AssertNoProjectionAccepted(result);
    }

    [TestMethod]
    public void TryPlanSource_WhenDirectoryNamePredicateIsUsed_AcceptsPredicate()
    {
        var schema = new OsSchema();
        var request = CreateRequest(Equal("d.Name", "Directory1"));

        var result = schema.TryPlanSource("directories", request, ".", false);

        Assert.IsNotNull(result.AcceptedPredicate);
        Assert.IsNull(result.ResidualPredicate);
        Assert.AreEqual(result.AcceptedPredicate, result.ExecutionPlan.AcceptedPredicate);
        AssertNoProjectionAccepted(result);
        Assert.IsTrue(result.ExecutionPlan.Properties.ContainsKey("OsDirectoryFilters"));
    }

    [TestMethod]
    public void TryPlanSource_WhenRequiredColumnsArePresent_DoesNotAcceptProjection()
    {
        var schema = new OsSchema();
        var request = CreateRequest(
            Equal("f.Extension", ".txt"),
            [new SourceColumnRef("Extension")]);

        var result = schema.TryPlanSource("files", request, ".", false);

        AssertNoProjectionAccepted(result);
    }

    private static SourcePlanRequest CreateRequest(
        SourcePredicateExpression predicate,
        IReadOnlyList<SourceColumnRef>? requiredColumns = null)
    {
        return new SourcePlanRequest
        {
            Identity = new SourceIdentity("os", "os", "os", "os"),
            RequiredColumns = requiredColumns ?? [],
            SourceRuntimeSettings = new Dictionary<string, string>(),
            Predicate = predicate,
            OrderBy = []
        };
    }

    private static SourcePredicateComparison Equal(string columnName, object value)
    {
        return Compare(SourcePredicateComparisonOperator.Equal, columnName, value);
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

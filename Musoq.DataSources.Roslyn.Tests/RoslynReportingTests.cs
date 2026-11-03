using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.Schema.Diagnostics;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public sealed class RoslynReportingTests
{
    [TestMethod]
    public void AnalysisTimeout_ReturnsExistingDefaultAndReportsWarning()
    {
        var warnings = new DataSourceWarningCollector();
        var scope = Scope(warnings);
        var value = RoslynAsyncHelper.RunSyncWithTimeout(async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return 1;
        }, TimeSpan.Zero, -1, scope);
        Assert.AreEqual(-1, value);
        var warning = warnings.Snapshot().Single();
        Assert.AreEqual("ROSLYN.AnalysisTimeout", warning.Arguments["providerCode"]);
        Assert.AreEqual("Timeout", warning.Arguments["failureKind"]);
        Assert.AreEqual("true", warning.Arguments["canRetry"]);
    }

    [TestMethod]
    public void QueryCancellation_IsNotAWarningOrDefaultValue()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var warnings = new DataSourceWarningCollector();
        var scope = new RoslynReporting(warnings.ForSource(SourceIdentity.Empty), cancellation.Token);
        Assert.ThrowsException<OperationCanceledException>(() =>
            RoslynAsyncHelper.RunSyncWithTimeout(_ => Task.FromResult(1), TimeSpan.FromSeconds(1), -1, scope));
        Assert.AreEqual(0, warnings.Snapshot().Count);
    }

    [TestMethod]
    public void IndependentAggregate_IsNotHiddenByTimeoutFallback()
    {
        var aggregate = new AggregateException(new OperationCanceledException(), new InvalidOperationException("PRIVATE"));
        var failure = Assert.ThrowsException<AggregateException>(() => RoslynAsyncHelper.RunSyncWithTimeout(
            _ => Task.FromException<int>(aggregate), TimeSpan.FromSeconds(1), -1, Scope(new DataSourceWarningCollector())));
        Assert.AreSame(aggregate, failure);
    }

    [TestMethod]
    public async Task QueryEntityGraph_RetainsItsOwnReportingScope()
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("test", LanguageNames.CSharp);
        workspace.AddDocument(project.Id, "sample.cs", SourceText.From("class C { int field; event System.Action Changed; void M() {} }"));
        var scope = Scope(new DataSourceWarningCollector());
        var solution = new SolutionEntity(workspace.CurrentSolution, new Mock<INuGetPackageMetadataRetriever>().Object,
            CancellationToken.None) { Reporting = scope };
        var document = solution.Projects.Single().Documents.Single();
        await document.InitializeAsync(CancellationToken.None);
        var type = document.Classes.Single();
        Assert.AreSame(scope, document.Reporting);
        Assert.AreSame(scope, type.Reporting);
        Assert.AreSame(scope, type.Methods.Single().Reporting);
        Assert.AreSame(scope, type.Fields.Single().Reporting);
        Assert.AreSame(scope, type.Events.Single().Reporting);
        var clone = solution.CloneWith(new Mock<INuGetPackageMetadataRetriever>().Object, CancellationToken.None);
        Assert.IsNull(clone.Reporting);
    }

    [TestMethod]
    public void IncompatibleRuntime_RetainsPrivateCauseAndReportsConfigurationFix()
    {
        var cause = new MissingMethodException("PRIVATE-METHOD");
        var wrapper = RoslynVersionHelper.CreateVersionMismatchException(cause);
        var error = (DataSourceException)wrapper.InnerException!;
        Assert.AreEqual("ROSLYN.IncompatibleRuntime", error.Report.Code);
        Assert.AreEqual(DataSourceFailureKind.InvalidConfiguration, error.Report.FailureKind);
        Assert.AreSame(cause, error.InnerException);
        Assert.IsFalse(error.Report.Message.Contains("PRIVATE-METHOD", StringComparison.Ordinal));
    }

    private static RoslynReporting Scope(DataSourceWarningCollector warnings)
        => new(warnings.ForSource(new SourceIdentity("#csharp", "solution", "ctx", "s")), CancellationToken.None);
}

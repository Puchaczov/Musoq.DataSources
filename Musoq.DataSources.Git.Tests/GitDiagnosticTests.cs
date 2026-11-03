using LibGit2Sharp;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class GitDiagnosticTests
{
    [TestMethod]
    public void MissingGitObject_ReportsResourceFailureWithoutPrivateSdkMessage()
    {
        var cause = new NotFoundException("PRIVATE-OBJECT");
        var report = GitDiagnostics.Map(cause)!;
        Assert.AreEqual("GIT.ResourceNotFound", report.Report.Code);
        Assert.AreSame(cause, report.InnerException);
        Assert.IsFalse(report.Report.Message.Contains("PRIVATE-OBJECT", StringComparison.Ordinal));
    }

    [TestMethod]
    public void InvalidRevision_ReportsArgumentWhileRetainingDirectArgumentContract()
    {
        var error = GitDiagnostics.InvalidRevision("PRIVATE-REVISION", "_revision");
        Assert.AreEqual("_revision", error.ParamName);
        var report = (DataSourceException)error.InnerException!;
        Assert.AreEqual("GIT.InvalidRevision", report.Report.Code);
        Assert.AreEqual("revision", report.Report.ArgumentName);
        Assert.AreEqual(2, report.Report.ArgumentIndex);
        Assert.IsFalse(report.Message.Contains("PRIVATE-REVISION", StringComparison.Ordinal));
    }
}

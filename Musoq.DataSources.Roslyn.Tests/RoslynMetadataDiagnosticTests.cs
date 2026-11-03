using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.Schema.Diagnostics;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public sealed class RoslynMetadataDiagnosticTests
{
    [TestMethod]
    public async Task InvalidCachedMetadata_PreservesNullFallbackAndReportsWarning()
    {
        var warnings = new DataSourceWarningCollector();
        var files = Files();
        files.Setup(x => x.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("<PRIVATE-MALFORMED-XML");
        var service = new NuGetRetrievalService(null, files.Object, null, Scope(warnings));
        Assert.IsNull(await service.GetMetadataFromPathAsync(Resource(), "ProjectUrl", CancellationToken.None));
        var warning = warnings.Snapshot().Single();
        Assert.AreEqual("ROSLYN.InvalidPackageMetadata.Fallback", warning.Arguments["providerCode"]);
        Assert.IsFalse(warning.Message.Contains("PRIVATE", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CachedMetadataDefect_IsNotSuppressed()
    {
        var files = Files();
        var cause = new InvalidOperationException("PRIVATE-DEFECT");
        files.Setup(x => x.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(cause);
        var service = new NuGetRetrievalService(null, files.Object, null);
        Assert.AreSame(cause, await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            service.GetMetadataFromPathAsync(Resource(), "ProjectUrl", CancellationToken.None)));
    }

    [TestMethod]
    public async Task CustomMetadataServiceTimeout_PreservesFallbackAndReportsSafeWarning()
    {
        var warnings = new DataSourceWarningCollector();
        var resources = Resource();
        var services = new Mock<INuGetRetrievalService>();
        services.Setup(x => x.GetMetadataFromCustomApiAsync(It.IsAny<string>(), It.IsAny<NuGetResource>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("PRIVATE-REMOTE-BODY"));
        var visitor = new NuGetResourceVisitor(resources, services.Object, "https://PRIVATE-ENDPOINT/",
            new Dictionary<string, HashSet<string>>(), ResolveValueStrategy.UseCustomApiOnly, NullLogger.Instance, Scope(warnings));
        await visitor.VisitProjectUrlAsync(CancellationToken.None);
        Assert.IsNull(resources.ProjectUrl);
        var warning = warnings.Snapshot().Single();
        Assert.AreEqual("ROSLYN.Timeout.Fallback", warning.Arguments["providerCode"]);
        Assert.AreEqual("true", warning.Arguments["canRetry"]);
        Assert.IsFalse(warning.Message.Contains("PRIVATE", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CachedMetadataCancellation_StopsBeforeAccess()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var files = new Mock<IFileSystem>(MockBehavior.Strict);
        var service = new NuGetRetrievalService(null, files.Object, null);
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
            service.GetMetadataFromPathAsync(Resource(), "ProjectUrl", cancellation.Token));
        files.VerifyNoOtherCalls();
    }

    private static Mock<IFileSystem> Files()
    {
        var files = new Mock<IFileSystem>();
        files.Setup(x => x.IsFileExists(It.IsAny<string>())).Returns(true);
        return files;
    }

    private static NuGetResource Resource() => new() { PackagePath = "cache", PackageName = "package", PackageVersion = "1.0.0" };
    private static RoslynReporting Scope(DataSourceWarningCollector warnings)
        => new(warnings.ForSource(new SourceIdentity("#csharp", "solution", "ctx", "s")), CancellationToken.None);
}

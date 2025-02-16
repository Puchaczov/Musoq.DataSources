using Moq;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public class NugetRetrievalTests
{
    [TestMethod]
    public async Task GetMetadataAsync_WithMockedFileSystem_ShouldReturnLicenseUrl()
    {
        // Arrange
        var fileSystemMock = new Mock<IFileSystem>();
        fileSystemMock.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        fileSystemMock.Setup(x => x.ReadAllText(It.IsAny<string>())).Returns("Mocked License Content");

        var retrievalServiceMock = new Mock<INuGetRetrievalService>();

        var cachePathResolverMock = new Mock<INuGetCachePathResolver>();
        cachePathResolverMock.Setup(x => x.ResolveAll()).Returns(["C:\\NugetCache"]);

        var retriever = new NuGetPackageMetadataRetriever(
            cachePathResolverMock.Object,
            null,
            retrievalServiceMock.Object);

        // Act
        var result = await retriever.GetMetadataAsync("TestPackage", "1.0.0", CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task GetMetadataAsync_WhenCacheFileIsMissing_ShouldUseNuGetOrgData()
    {
        // Arrange
        var fileSystemMock = new Mock<IFileSystem>();
        fileSystemMock.Setup(x => x.Exists(It.IsAny<string>())).Returns(false);

        var retrievalServiceMock = new Mock<INuGetRetrievalService>();
        retrievalServiceMock
            .Setup(x => x.GetMetadataFromWebAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CommonResources>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("MockedNuGetOrgValue");

        var cachePathResolverMock = new Mock<INuGetCachePathResolver>();
        cachePathResolverMock.Setup(x => x.ResolveAll()).Returns(["C:\\NugetCache"]);

        var retriever = new NuGetPackageMetadataRetriever(
            cachePathResolverMock.Object,
            null,
            retrievalServiceMock.Object);

        // Act
        var result = await retriever.GetMetadataAsync("TestPackageMissing", "2.0.0", CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("MockedNuGetOrgValue", result[nameof(CommonResources.LicenseUrl)]);
    }

    [TestMethod]
    public async Task GetMetadataAsync_NoCacheAndNoNuGetOrgData_ShouldCallCustomApi()
    {
        // Arrange
        var fileSystemMock = new Mock<IFileSystem>();
        fileSystemMock.Setup(x => x.Exists(It.IsAny<string>())).Returns(false);

        var retrievalServiceMock = new Mock<INuGetRetrievalService>();
        retrievalServiceMock
            .Setup(x => x.GetMetadataFromWebAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CommonResources>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        retrievalServiceMock
            .Setup(x => x.GetMetadataFromCustomApiAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("MeaningfulCustomApiValue");

        var cachePathResolverMock = new Mock<INuGetCachePathResolver>();
        cachePathResolverMock.Setup(x => x.ResolveAll()).Returns(["C:\\NugetCache"]);

        var retriever = new NuGetPackageMetadataRetriever(
            cachePathResolverMock.Object,
            "http://somecustomapi/v1",
            retrievalServiceMock.Object);

        // Act
        var result = await retriever.GetMetadataAsync("PackageNoCacheNoNuGetData", "3.0.0", CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("MeaningfulCustomApiValue", result[nameof(CommonResources.LicenseUrl)]);
    }

    [TestMethod]
    public async Task GetMetadataAsync_WhenCancellationRequested_ShouldStopProcessing()
    {
        // Arrange
        var retrievalServiceMock = new Mock<INuGetRetrievalService>();
        var cachePathResolverMock = new Mock<INuGetCachePathResolver>();
        cachePathResolverMock.Setup(x => x.ResolveAll()).Returns(["C:\\NugetCache"]);

        var cts = new CancellationTokenSource();
        var retriever = new NuGetPackageMetadataRetriever(
            cachePathResolverMock.Object,
            null,
            retrievalServiceMock.Object);

        // Act
        await cts.CancelAsync();
        var result = await retriever.GetMetadataAsync("TestPackage", "1.0.0", cts.Token);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.Any(x => x.Value != null));
        retrievalServiceMock.Verify(x => x.GetMetadataFromWebAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CommonResources>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GetMetadataAsync_WithAllPropertiesAvailable_ShouldReturnComplete()
    {
        // Arrange
        var retrievalServiceMock = new Mock<INuGetRetrievalService>();
        var cachePathResolverMock = new Mock<INuGetCachePathResolver>();
        cachePathResolverMock.Setup(x => x.ResolveAll()).Returns(["C:\\NugetCache"]);
        
        retrievalServiceMock
            .Setup(x => x.GetMetadataFromPathAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, string package, string property, CancellationToken token) => 
                $"Local_{property}");

        var retriever = new NuGetPackageMetadataRetriever(
            cachePathResolverMock.Object,
            null,
            retrievalServiceMock.Object);

        // Act
        var result = await retriever.GetMetadataAsync("TestPackage", "1.0.0", CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Local_LicenseUrl", result[nameof(CommonResources.LicenseUrl)]);
        Assert.AreEqual("Local_ProjectUrl", result[nameof(CommonResources.ProjectUrl)]);
        Assert.AreEqual("Local_Description", result[nameof(CommonResources.Description)]);
    }

    [TestMethod]
    public async Task GetMetadataAsync_WithInvalidPackagePath_ShouldFallbackToWeb()
    {
        // Arrange
        var retrievalServiceMock = new Mock<INuGetRetrievalService>();
        var cachePathResolverMock = new Mock<INuGetCachePathResolver>();
        cachePathResolverMock.Setup(x => x.ResolveAll()).Returns(["C:\\NugetCache"]);

        retrievalServiceMock
            .Setup(x => x.GetMetadataFromWebAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CommonResources>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Web_Value");

        var retriever = new NuGetPackageMetadataRetriever(
            cachePathResolverMock.Object,
            null,
            retrievalServiceMock.Object);

        // Act
        var result = await retriever.GetMetadataAsync("InvalidPackage", "1.0.0", CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Web_Value", result[nameof(CommonResources.LicenseUrl)]);
    }

    [TestMethod]
    public async Task GetMetadataAsync_WithBooleanProperty_ShouldParseCorrectly()
    {
        // Arrange
        var retrievalServiceMock = new Mock<INuGetRetrievalService>();
        var cachePathResolverMock = new Mock<INuGetCachePathResolver>();
        cachePathResolverMock.Setup(x => x.ResolveAll()).Returns(["C:\\NugetCache"]);

        retrievalServiceMock
            .Setup(x => x.GetMetadataFromPathAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                nameof(CommonResources.RequireLicenseAcceptance),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("true");

        var retriever = new NuGetPackageMetadataRetriever(
            cachePathResolverMock.Object,
            null,
            retrievalServiceMock.Object);

        // Act
        var result = await retriever.GetMetadataAsync("TestPackage", "1.0.0", CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("True", result[nameof(CommonResources.RequireLicenseAcceptance)]);
    }

    [TestMethod]
    public async Task GetMetadataAsync_WithCustomApiAndInvalidResponse_ShouldHandleGracefully()
    {
        // Arrange
        var retrievalServiceMock = new Mock<INuGetRetrievalService>();
        var cachePathResolverMock = new Mock<INuGetCachePathResolver>();
        cachePathResolverMock.Setup(x => x.ResolveAll()).Returns(["C:\\NugetCache"]);

        retrievalServiceMock
            .Setup(x => x.GetMetadataFromCustomApiAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API Error"));

        var retriever = new NuGetPackageMetadataRetriever(
            cachePathResolverMock.Object,
            "http://invalid-api",
            retrievalServiceMock.Object);

        // Act
        var result = await retriever.GetMetadataAsync("TestPackage", "1.0.0", CancellationToken.None);

        // Assert
        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(
            expected: new string?[] { null },
            actual: result.Values.Distinct().ToArray(),
            "All values should be null when API fails");
        
        retrievalServiceMock.Verify(
            x => x.GetMetadataFromCustomApiAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "Custom API should be called at least once");
    }
}
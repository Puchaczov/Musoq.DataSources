using Microsoft.Extensions.Logging;
using Moq;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using CollectionAssert = Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public class NugetRetrievalTests
{
    [TestMethod]
    public async Task GetMetadataAsync_WithMockedFileSystem_ShouldReturnLicenseUrl()
    {
        // Arrange
        var fileSystemMock = new Mock<IFileSystem>();
        fileSystemMock.Setup(x => x.IsFileExists(It.IsAny<string>())).Returns(false);
        fileSystemMock.Setup(x => x.IsDirectoryExists(It.IsAny<string>())).Returns(true);
        fileSystemMock.Setup(x => x.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("Mocked License Content");

        var retrievalServiceMock = new Mock<INuGetRetrievalService>();

        var cachePathResolverMock = new Mock<INuGetCachePathResolver>();
        cachePathResolverMock.Setup(x => x.ResolveAll()).Returns(["C:\\NugetCache"]);

        var packageVersionConcurrencyManager = new PackageVersionConcurrencyManager();
        var logger = new Mock<ILogger>();

        var retriever = new NuGetPackageMetadataRetriever(
            cachePathResolverMock.Object,
            null,
            retrievalServiceMock.Object,
            fileSystemMock.Object,
            packageVersionConcurrencyManager,
            logger.Object);

        // Act
        var enumerator = retriever.GetMetadataAsync("TestPackage", "1.0.0", CancellationToken.None).GetAsyncEnumerator();
        await enumerator.MoveNextAsync();
        var result = enumerator.Current;

        // Assert
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task GetMetadataAsync_WhenCacheFileIsMissing_ShouldUseNuGetOrgData()
    {
        // Arrange
        var fileSystemMock = new Mock<IFileSystem>();
        fileSystemMock.Setup(x => x.IsFileExists(It.IsAny<string>())).Returns(false);
        fileSystemMock.Setup(x => x.IsDirectoryExists(It.IsAny<string>())).Returns(false);

        var retrievalServiceMock = new Mock<INuGetRetrievalService>();
        
        retrievalServiceMock
            .Setup(x => x.DownloadPackageAsync(
                It.IsAny<string>(), 
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)"/some/example/path");
        
        retrievalServiceMock.Setup(x => x.GetMetadataFromPathAsync(It.IsAny<NuGetResource>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NuGetResource _, string propertyName, CancellationToken _) =>
            {
                if (propertyName == "LicensesNames")
                {
                    return "[]";
                }

                return null;
            });
        
        retrievalServiceMock
            .Setup(x => x.GetMetadataFromNugetOrgAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetResource>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, NuGetResource _, string propertyName, CancellationToken _) =>
            {
                if (propertyName == "LicensesNames")
                {
                    return "[\"MIT\"]";
                }

                if (propertyName == nameof(NuGetLicense.LicenseContent))
                {
                    return "1";
                }
                
                if (propertyName == nameof(NuGetLicense.LicenseUrl))
                {
                    return "2";
                }
                
                return "MockedNuGetOrgValue";
            });

        var cachePathResolverMock = new Mock<INuGetCachePathResolver>();
        cachePathResolverMock.Setup(x => x.ResolveAll()).Returns(["C:\\NugetCache"]);
        
        var packageVersionConcurrencyManager = new PackageVersionConcurrencyManager();
        var logger = new Mock<ILogger>();

        var retriever = new NuGetPackageMetadataRetriever(
            cachePathResolverMock.Object,
            null,
            retrievalServiceMock.Object,
            fileSystemMock.Object,
            packageVersionConcurrencyManager,
            logger.Object);

        // Act
        var enumerator = retriever.GetMetadataAsync("TestPackageMissing", "2.0.0", CancellationToken.None).GetAsyncEnumerator();
        await enumerator.MoveNextAsync();
        var result = enumerator.Current;

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("MIT", result[nameof(NuGetLicense.License)]);
        Assert.AreEqual("1", result[nameof(NuGetLicense.LicenseContent)]);
        Assert.AreEqual("2", result[nameof(NuGetLicense.LicenseUrl)]);
    }

    [TestMethod]
    public async Task GetMetadataAsync_NoCacheAndNoNuGetOrgData_ShouldCallCustomApi()
    {
        // Arrange
        var fileSystemMock = new Mock<IFileSystem>();
        fileSystemMock.Setup(x => x.IsFileExists(It.IsAny<string>())).Returns(false);
        fileSystemMock.Setup(x => x.IsDirectoryExists(It.IsAny<string>())).Returns(false);

        var retrievalServiceMock = new Mock<INuGetRetrievalService>();

        retrievalServiceMock.Setup(x =>
                x.GetMetadataFromPathAsync(It.IsAny<NuGetResource>(), It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((NuGetResource _, string propertyName, CancellationToken _) =>
            {
                if (propertyName == "LicensesNames")
                {
                    return "[]";
                }

                return null;
            });
        
        retrievalServiceMock
            .Setup(x => x.GetMetadataFromNugetOrgAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetResource>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, NuGetResource _, string propertyName,
                CancellationToken _) =>
            {
                if (propertyName == "LicensesNames")
                {
                    return "[]";
                }

                return null;
            });

        retrievalServiceMock
            .Setup(x => x.DownloadPackageAsync(
                It.IsAny<string>(), 
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)"/some/example/path");

        retrievalServiceMock
            .Setup(x => x.GetMetadataFromCustomApiAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetResource>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, NuGetResource _, string propertyName, CancellationToken _) =>
            {
                if (propertyName == "LicensesNames")
                {
                    return "[\"MIT\"]";
                }

                if (propertyName == nameof(NuGetLicense.LicenseContent))
                {
                    return "1";
                }
                
                if (propertyName == nameof(NuGetLicense.LicenseUrl))
                {
                    return "2";
                }
                
                return "MockedCustomApiValue";
            });

        var cachePathResolverMock = new Mock<INuGetCachePathResolver>();
        cachePathResolverMock.Setup(x => x.ResolveAll()).Returns(["C:\\NugetCache"]);
        
        var packageVersionConcurrencyManager = new PackageVersionConcurrencyManager();
        var logger = new Mock<ILogger>();

        var retriever = new NuGetPackageMetadataRetriever(
            cachePathResolverMock.Object,
            "http://somecustomapi/v1",
            retrievalServiceMock.Object,
            fileSystemMock.Object,
            packageVersionConcurrencyManager,
            logger.Object);

        // Act
        var enumerator = retriever.GetMetadataAsync("PackageNoCacheNoNuGetData", "3.0.0", CancellationToken.None).GetAsyncEnumerator();
        await enumerator.MoveNextAsync();
        var result = enumerator.Current;

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("MIT", result[nameof(NuGetLicense.License)]);
        Assert.AreEqual("1", result[nameof(NuGetLicense.LicenseContent)]);
        Assert.AreEqual("2", result[nameof(NuGetLicense.LicenseUrl)]);
    }

    [TestMethod]
    public async Task GetMetadataAsync_WhenCancellationRequested_ShouldStopProcessing()
    {
        // Arrange
        var retrievalServiceMock = new Mock<INuGetRetrievalService>();
        var cachePathResolverMock = new Mock<INuGetCachePathResolver>();
        var fileSystemMock = new Mock<IFileSystem>();
        
        cachePathResolverMock.Setup(x => x.ResolveAll()).Returns(["C:\\NugetCache"]);
        fileSystemMock.Setup(x => x.IsFileExists(It.IsAny<string>())).Returns(false);
        fileSystemMock.Setup(x => x.IsDirectoryExists(It.IsAny<string>())).Returns(false);
        
        var packageVersionConcurrencyManager = new PackageVersionConcurrencyManager();
        var logger = new Mock<ILogger>();

        var cts = new CancellationTokenSource();
        var retriever = new NuGetPackageMetadataRetriever(
            cachePathResolverMock.Object,
            null,
            retrievalServiceMock.Object,
            fileSystemMock.Object,
            packageVersionConcurrencyManager,
            logger.Object);

        // Act
        IAsyncEnumerator<IReadOnlyDictionary<string, string?>>? enumerator = null;
        try
        {
            await cts.CancelAsync();
            enumerator = retriever.GetMetadataAsync("TestPackage", "1.0.0", cts.Token).GetAsyncEnumerator(cts.Token);
            await enumerator.MoveNextAsync();
            await enumerator.MoveNextAsync();
        }
        catch (OperationCanceledException)
        {
        }
        
        var result = enumerator?.Current;

        // Assert
        Assert.IsNotNull(enumerator);
        Assert.IsNull(result);
        retrievalServiceMock.Verify(x => x.GetMetadataFromNugetOrgAsync(
            It.IsAny<string>(),
            It.IsAny<NuGetResource>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GetMetadataAsync_WithAllPropertiesAvailable_ShouldReturnComplete()
    {
        // Arrange
        var retrievalServiceMock = new Mock<INuGetRetrievalService>();
        var cachePathResolverMock = new Mock<INuGetCachePathResolver>();
        var fileSystemMock = new Mock<IFileSystem>();
        
        cachePathResolverMock.Setup(x => x.ResolveAll()).Returns(["C:\\NugetCache"]);
        fileSystemMock.Setup(x => x.IsDirectoryExists(It.IsAny<string>())).Returns(true);
        fileSystemMock.Setup(x => x.IsFileExists(It.IsAny<string>())).Returns(false);
        
        retrievalServiceMock.Setup(f => f.DownloadPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)"/some/example/path");
        
        retrievalServiceMock
            .Setup(x => x.GetMetadataFromPathAsync(
                It.IsAny<NuGetResource>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((NuGetResource _, string property, CancellationToken _) =>
            {
                if (property == "LicensesNames")
                    return "[\"MIT\"]";
                
                if (property == nameof(NuGetLicense.LicenseContent))
                    return "1";
                
                if (property == nameof(NuGetLicense.LicenseUrl))
                    return "2";
                
                return $"Local_{property}";
            });
        
        var packageVersionConcurrencyManager = new PackageVersionConcurrencyManager();
        var logger = new Mock<ILogger>();

        var retriever = new NuGetPackageMetadataRetriever(
            cachePathResolverMock.Object,
            null,
            retrievalServiceMock.Object,
            fileSystemMock.Object,
            packageVersionConcurrencyManager,
            logger.Object);

        // Act
        var enumerator = retriever.GetMetadataAsync("TestPackage", "1.0.0", CancellationToken.None).GetAsyncEnumerator();
        await enumerator.MoveNextAsync();
        var result = enumerator.Current;

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("MIT", result[nameof(NuGetLicense.License)]);
        Assert.AreEqual("1", result[nameof(NuGetLicense.LicenseContent)]);
        Assert.AreEqual("2", result[nameof(NuGetLicense.LicenseUrl)]);
        Assert.AreEqual("Local_ProjectUrl", result[nameof(NuGetResource.ProjectUrl)]);
        Assert.AreEqual("Local_Description", result[nameof(NuGetResource.Description)]);
    }

    [TestMethod]
    public async Task GetMetadataAsync_WithInvalidPackagePath_ShouldFallbackToWeb()
    {
        // Arrange
        var retrievalServiceMock = new Mock<INuGetRetrievalService>();
        var cachePathResolverMock = new Mock<INuGetCachePathResolver>();
        var fileSystemMock = new Mock<IFileSystem>();
        
        cachePathResolverMock.Setup(x => x.ResolveAll()).Returns(["C:\\NugetCache"]);
        fileSystemMock.Setup(x => x.IsFileExists(It.IsAny<string>())).Returns(false);
        fileSystemMock.Setup(x => x.IsDirectoryExists(It.IsAny<string>())).Returns(false);
        
        retrievalServiceMock.Setup(f => f.DownloadPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)"/some/example/path");
        
        retrievalServiceMock.Setup(f => f.GetMetadataFromPathAsync(It.IsAny<NuGetResource>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NuGetResource _, string propertyName, CancellationToken _) =>
            {
                if (propertyName == "LicensesNames")
                {
                    return "[]";
                }
                
                return null;
            });

        retrievalServiceMock
            .Setup(x => x.GetMetadataFromNugetOrgAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetResource>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, NuGetResource _, string propertyName, CancellationToken _) =>
            {
                if (propertyName == "LicensesNames")
                {
                    return "[\"MIT\"]";
                }
                
                if (propertyName == nameof(NuGetLicense.LicenseContent))
                {
                    return "1";
                }
                
                if (propertyName == nameof(NuGetLicense.LicenseUrl))
                {
                    return "2";
                }

                return "MockedNuGetOrgValue";
            });
        
        var packageVersionConcurrencyManager = new PackageVersionConcurrencyManager();
        var logger = new Mock<ILogger>();

        var retriever = new NuGetPackageMetadataRetriever(
            cachePathResolverMock.Object,
            null,
            retrievalServiceMock.Object,
            fileSystemMock.Object,
            packageVersionConcurrencyManager,
            logger.Object);

        // Act
        var enumerator = retriever.GetMetadataAsync("InvalidPackage", "1.0.0", CancellationToken.None).GetAsyncEnumerator();
        await enumerator.MoveNextAsync();
        var result = enumerator.Current;

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("MIT", result[nameof(NuGetLicense.License)]);
        Assert.AreEqual("1", result[nameof(NuGetLicense.LicenseContent)]);
        Assert.AreEqual("2", result[nameof(NuGetLicense.LicenseUrl)]);
    }

    [TestMethod]
    public async Task GetMetadataAsync_WithBooleanProperty_ShouldParseCorrectly()
    {
        // Arrange
        var retrievalServiceMock = new Mock<INuGetRetrievalService>();
        var cachePathResolverMock = new Mock<INuGetCachePathResolver>();
        var fileSystemMock = new Mock<IFileSystem>();
        
        cachePathResolverMock.Setup(x => x.ResolveAll()).Returns(["C:\\NugetCache"]);
        fileSystemMock.Setup(x => x.IsDirectoryExists(It.IsAny<string>())).Returns(true);
        fileSystemMock.Setup(x => x.IsFileExists(It.IsAny<string>())).Returns(false);

        retrievalServiceMock
            .Setup(x => x.GetMetadataFromPathAsync(
                It.IsAny<NuGetResource>(),
                nameof(NuGetResource.RequireLicenseAcceptance),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("true");
        
        var packageVersionConcurrencyManager = new PackageVersionConcurrencyManager();
        var logger = new Mock<ILogger>();

        var retriever = new NuGetPackageMetadataRetriever(
            cachePathResolverMock.Object,
            null,
            retrievalServiceMock.Object,
            fileSystemMock.Object,
            packageVersionConcurrencyManager,
            logger.Object);

        // Act
        var enumerator = retriever.GetMetadataAsync("TestPackage", "1.0.0", CancellationToken.None).GetAsyncEnumerator();
        await enumerator.MoveNextAsync();
        var result = enumerator.Current;

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("True", result[nameof(NuGetResource.RequireLicenseAcceptance)]);
    }

    [TestMethod]
    public async Task GetMetadataAsync_WithCustomApiAndInvalidResponse_ShouldHandleGracefully()
    {
        // Arrange
        var retrievalServiceMock = new Mock<INuGetRetrievalService>();
        var cachePathResolverMock = new Mock<INuGetCachePathResolver>();
        var fileSystemMock = new Mock<IFileSystem>();
        
        cachePathResolverMock.Setup(x => x.ResolveAll()).Returns(["C:\\NugetCache"]);
        fileSystemMock.Setup(x => x.IsFileExists(It.IsAny<string>())).Returns(false);
        fileSystemMock.Setup(x => x.IsDirectoryExists(It.IsAny<string>())).Returns(false);

        retrievalServiceMock
            .Setup(x => x.GetMetadataFromCustomApiAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetResource>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API Error"));
        
        var packageVersionConcurrencyManager = new PackageVersionConcurrencyManager();
        var logger = new Mock<ILogger>();

        var retriever = new NuGetPackageMetadataRetriever(
            cachePathResolverMock.Object,
            "http://invalid-api",
            retrievalServiceMock.Object,
            fileSystemMock.Object,
            packageVersionConcurrencyManager,
            logger.Object);

        // Act
        var enumerator = retriever.GetMetadataAsync("TestPackage", "1.0.0", CancellationToken.None).GetAsyncEnumerator();
        await enumerator.MoveNextAsync();
        var result = enumerator.Current;

        // Assert
        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(
            expected: new string?[] { null },
            actual: result.Values.Distinct().ToArray(),
            "All values should be null when API fails");
        
        retrievalServiceMock.Verify(
            x => x.GetMetadataFromCustomApiAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetResource>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "Custom API should be called at least once");
    }
}
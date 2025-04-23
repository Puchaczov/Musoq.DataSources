using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public class NugetPackageMetadataRetrieverPlaygroundTests
{
    [Ignore]
    [TestMethod]
    public async Task Playground_GetDependenciesAsync()
    {
        // Assert
        var client = new DefaultHttpClient(() => new HttpClient());
        var fileSystem = new DefaultFileSystem();
        
        // Arrange
        var retriever = new NuGetPackageMetadataRetriever(
            new NuGetCachePathResolver(@"D:\repos\Musoq.Cloud\src\dotnet\Musoq.Cloud.sln", OSPlatform.Windows),
            null,
            new NuGetRetrievalService(
                new NuGetPropertiesResolver("https://localhost:7137", client),
                fileSystem,
                client
            ),
            fileSystem,
            new PackageVersionConcurrencyManager(),
            new Dictionary<string, HashSet<string>>
            {
                { "LicenseUrl", ["https://aka.ms/deprecateLicenseUrl"] }
            },
            NullLogger.Instance
        );
        var packageName = "CsvHelper";
        var version = "33.0.1";

        var deps = new List<DependencyInfo>();
        // Act
        await foreach (var metadata in retriever.GetDependenciesAsync(packageName, version, CancellationToken.None))
        {
            deps.Add(metadata);
        }
    }
    
    [Ignore]
    [TestMethod]
    public async Task Playground_GetMetadataAsync()
    {
        // Assert
        var client = new DefaultHttpClient(() => new HttpClient());
        var fileSystem = new DefaultFileSystem();
        
        // Arrange
        var retriever = new NuGetPackageMetadataRetriever(
            new NuGetCachePathResolver(@"D:\repos\Musoq.Cloud\src\dotnet\Musoq.Cloud.sln", OSPlatform.Windows),
            null,
            new NuGetRetrievalService(
                new NuGetPropertiesResolver("https://localhost:7137", client),
                fileSystem,
                client
            ),
            fileSystem,
            new PackageVersionConcurrencyManager(),
            new Dictionary<string, HashSet<string>>
            {
                { "LicenseUrl", ["https://aka.ms/deprecateLicenseUrl"] }
            },
            NullLogger.Instance
        );
        var packageName = "System.Memory";
        var version = "4.6.3";

        // Act
        await foreach (var metadata in retriever.GetMetadataAsync(packageName, version, CancellationToken.None))
        {
            
        }
    }
}
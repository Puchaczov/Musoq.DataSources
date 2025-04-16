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
    public async Task Playground()
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

        // Act
        await foreach (var metadata in retriever.GetMetadataAsync(packageName, version, CancellationToken.None))
        {
            
        }
    }
}
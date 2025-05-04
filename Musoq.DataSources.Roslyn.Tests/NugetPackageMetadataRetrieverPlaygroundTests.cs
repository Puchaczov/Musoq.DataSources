using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.DataSources.Roslyn.Components.NuGet.Http.Handlers;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public class NugetPackageMetadataRetrieverPlaygroundTests
{
    [Ignore]
    [TestMethod]
    public async Task Playground_GetDependenciesAsync()
    {
        // Assert
        var client = new DefaultHttpClient(
            () => new HttpClient(
                new SingleQueryCacheResponseHandler()
            )
        );
        var fileSystem = new DefaultFileSystem();
        
        // Arrange
        var retriever = new NuGetPackageMetadataRetriever(
            new NuGetCachePathResolver(@"D:\repos\Musoq.Cloud\src\dotnet\Musoq.Cloud.sln", OSPlatform.Windows, NullLogger.Instance),
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
            ResolveValueStrategy.UseNugetOrgApiOnly,
            NullLogger.Instance
        );
        var packageName = "Microsoft.EntityFrameworkCore.Design";
        var version = "9.0.4";

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
        var client = new DefaultHttpClient(
            () => new HttpClient(
                new PersistentCacheResponseHandler(
                    "C:\\Users\\Jakub\\AppData\\Local\\Temp\\DataSourcesCache\\Musoq.DataSources.Roslyn\\NuGet",
                    new SingleQueryCacheResponseHandler(),
                    NullLogger.Instance
                )
            )
        );
        var fileSystem = new DefaultFileSystem();
        
        // Arrange
        var retriever = new NuGetPackageMetadataRetriever(
            new NuGetCachePathResolver(@"D:\repos\Musoq.Cloud\src\dotnet\Musoq.Cloud.sln", OSPlatform.Windows, NullLogger.Instance),
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
            ResolveValueStrategy.UseNugetOrgApiOnly,
            NullLogger.Instance
        );
        var packageName = "SQLitePCLRaw.bundle_e_sqlite3";
        var version = "2.1.6";

        var metadata = new List<IReadOnlyDictionary<string, string?>>();
        
        // Act
        await foreach (var row in retriever.GetMetadataAsync(packageName, version, CancellationToken.None))
        {
            metadata.Add(row);
        }
    }
}
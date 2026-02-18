using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Musoq.DataSources.Roslyn.CliCommands;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.DataSources.Roslyn.Components.NuGet.Http.Handlers;
using Musoq.DataSources.Roslyn.Entities;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public class NugetResolveRawTests
{
    [Ignore]
    [TestMethod]
    public async Task SolutionPlayground()
    {
        var cacheDirectory = CSharpSchema.DefaultNugetCacheDirectoryPath;
        var httpClientHandler = new PersistentCacheResponseHandler(cacheDirectory, new SingleQueryCacheResponseHandler(
            new DomainRateLimitingHandler(
                new Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>
                {
                    {
                        "api.github.com",
                        new DomainRateLimitingHandler.DomainRateLimitConfig(60, TimeSpan.FromMinutes(1), 0)
                    },
                    {
                        "api.gitlab.com",
                        new DomainRateLimitingHandler.DomainRateLimitConfig(60, TimeSpan.FromMinutes(1), 0)
                    },
                    {
                        "api.nuget.org",
                        new DomainRateLimitingHandler.DomainRateLimitConfig(60, TimeSpan.FromMinutes(1), 0)
                    },
                    {
                        "www.nuget.org",
                        new DomainRateLimitingHandler.DomainRateLimitConfig(45, TimeSpan.FromMinutes(1), 0)
                    }
                },
                new DomainRateLimitingHandler.DomainRateLimitConfig(
                    10,
                    TimeSpan.FromSeconds(1),
                    10), false, NullLogger.Instance)), NullLogger.Instance);

        var httpClient = new DefaultHttpClient(() => new HttpClient(httpClientHandler));
        var fileSystem = new DefaultFileSystem();
        var solutionFilePath = "D:\\repos\\Musoq.Cloud\\src\\dotnet\\Musoq.Cloud.sln";
        var withTransitivePackages = true;
        var solutionEntity = await CreateSolutionAsync(solutionFilePath, httpClient, fileSystem, null,
            new NuGetPropertiesResolver("https://localhost:7137", httpClient), NullLogger.Instance,
            CancellationToken.None);

        await Parallel.ForEachAsync(solutionEntity.Projects, CancellationToken.None, async (project, token) =>
        {
            foreach (var document in project.Documents) await document.InitializeAsync(token);
        });

        var projects = solutionEntity.Projects;
        var projectsLibraries = new ConcurrentDictionary<string, IReadOnlyList<NugetPackageEntity>>();

        await Parallel.ForEachAsync(projects, async (project, token) =>
        {
            var packages = await project.GetNugetPackagesAsync(project.Project, withTransitivePackages);

            var projectName = project.Project.Name;

            projectsLibraries.TryAdd(projectName, packages);
        });
    }

    private async Task<SolutionEntity> CreateSolutionAsync(string solutionFilePath, IHttpClient? httpClient,
        IFileSystem? fileSystem, string? nugetPropertiesResolveEndpoint,
        INuGetPropertiesResolver nugetPropertiesResolver, ILogger logger, CancellationToken cancellationToken)
    {
        var workspace = MSBuildWorkspace.Create();
        var solutionLoadLogger = new SolutionLoadLogger(logger);
        var projectLoadProgressLogger = new ProjectLoadProgressLogger(logger);
        var solution = await workspace.OpenSolutionAsync(solutionFilePath, solutionLoadLogger,
            projectLoadProgressLogger, cancellationToken);
        var packageVersionConcurrencyManager = new PackageVersionConcurrencyManager();
        var nuGetPackageMetadataRetriever = new NuGetPackageMetadataRetriever(
            new NuGetCachePathResolver(
                solutionFilePath,
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatform.Windows : OSPlatform.Linux,
                logger
            ),
            nugetPropertiesResolveEndpoint,
            new NuGetRetrievalService(
                nugetPropertiesResolver,
                fileSystem,
                httpClient),
            fileSystem,
            packageVersionConcurrencyManager,
            SolutionOperationsCommand.BannedPropertiesValues,
            SolutionOperationsCommand.ResolveValueStrategy,
            logger
        );
        return new SolutionEntity(solution, nuGetPackageMetadataRetriever, cancellationToken);
    }
}
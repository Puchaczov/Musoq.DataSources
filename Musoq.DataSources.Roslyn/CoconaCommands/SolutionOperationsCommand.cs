using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Configuration;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet.Http;

namespace Musoq.DataSources.Roslyn.CoconaCommands;

internal class SolutionOperationsCommand
{
    // This cannot be AppContext.BaseDirectory as it must point the plugin directory
    private static readonly string RateLimitingOptionsFilePath = IFileSystem.Combine(new FileInfo(typeof(SolutionOperationsCommand).Assembly.Location).DirectoryName!, "RateLimitingOptions.json");
    
    internal static readonly ConcurrentDictionary<string, Solution> Solutions = new();
    internal static IReadOnlyDictionary<DomainRateLimitingHandler.DomainRateLimitingConfigKey, DomainRateLimitingHandler.DomainRateLimitConfig>? RateLimitingOptions;
    internal static string DefaultHttpClientCacheDirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "DataSourcesCache", "Musoq.DataSources.Roslyn", "NuGet");
    
    public async Task LoadAsync(string solutionFilePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(solutionFilePath, cancellationToken: cancellationToken);
        
        await Parallel.ForEachAsync(solution.Projects, cancellationToken, async (project, outerToken) =>
        {
            await Parallel.ForEachAsync(project.Documents, outerToken, async (document, innerToken) =>
            {
                await document.GetSyntaxTreeAsync(innerToken);
                await document.GetSemanticModelAsync(innerToken);
            });
        });

        Solutions.TryAdd(solutionFilePath, solution);
        RateLimitingOptions ??= await ReadDomainRateLimitingOptionsAsync(cancellationToken);
    }
    
    public Task UnloadAsync(string solutionFilePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        Solutions.TryRemove(solutionFilePath, out _);
            
        return Task.CompletedTask;
    }

    public static void Initialize()
    {
        using CancellationTokenSource cts = new();
        cts.CancelAfter(TimeSpan.FromMinutes(1));
        RateLimitingOptions ??= ReadDomainRateLimitingOptionsAsync(cts.Token).GetAwaiter().GetResult();
    }

    private static Task<IReadOnlyDictionary<DomainRateLimitingHandler.DomainRateLimitingConfigKey, DomainRateLimitingHandler.DomainRateLimitConfig>> ReadDomainRateLimitingOptionsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(RateLimitingOptionsFilePath);
        
        var rateLimitingOptions = configuration.Build();
        var unauthorizedSection = rateLimitingOptions.GetSection("Unauthorized");
        var authorizedSection = rateLimitingOptions.GetSection("Authorized");
        
        (string Name, bool WhenApiKeyPresent)[] domains = 
            unauthorizedSection.GetChildren().Select(f => (f.Key, false))
                .Concat(authorizedSection.GetChildren().Select(f => (f.Key, true))).ToArray();
        
        var domainRateLimitingOptions = new Dictionary<DomainRateLimitingHandler.DomainRateLimitingConfigKey, DomainRateLimitingHandler.DomainRateLimitConfig>();
        
        foreach (var domain in domains)
        {
            var domainRateLimitConfig = domain.WhenApiKeyPresent 
                ? ReadDomainRateLimitConfig("Authorized") 
                : ReadDomainRateLimitConfig("Unauthorized");

            if (domainRateLimitConfig != null)
            {
                domainRateLimitingOptions[new DomainRateLimitingHandler.DomainRateLimitingConfigKey(domain.Name, domain.WhenApiKeyPresent)] = domainRateLimitConfig;
            }

            continue;

            DomainRateLimitingHandler.DomainRateLimitConfig? ReadDomainRateLimitConfig(string sectionName)
            {
                var domainRateLimitingOptionsSection = rateLimitingOptions.GetSection($"{sectionName}:{domain.Name}");

                if (!domainRateLimitingOptionsSection.Exists()) 
                    return null;
                
                var permitsPerPeriod = domainRateLimitingOptionsSection.GetValue<int>("PermitsPerPeriod");
                var replenishmentPeriod = domainRateLimitingOptionsSection.GetValue<TimeSpan>("ReplenishmentPeriod");
                var queueLimit = domainRateLimitingOptionsSection.GetValue<int>("QueueLimit");

                return new DomainRateLimitingHandler.DomainRateLimitConfig(permitsPerPeriod, replenishmentPeriod, queueLimit);
            }
        }
        
        return Task.FromResult<IReadOnlyDictionary<DomainRateLimitingHandler.DomainRateLimitingConfigKey, DomainRateLimitingHandler.DomainRateLimitConfig>>(domainRateLimitingOptions);
    }
}
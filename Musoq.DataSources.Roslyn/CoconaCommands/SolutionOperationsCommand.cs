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
using Microsoft.Extensions.Logging;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet.Http;

namespace Musoq.DataSources.Roslyn.CoconaCommands;

internal class SolutionOperationsCommand(ILogger logger)
{
    // This cannot be AppContext.BaseDirectory as it must point to the plugin directory
    private static readonly string RateLimitingOptionsFilePath = IFileSystem.Combine(new FileInfo(typeof(SolutionOperationsCommand).Assembly.Location).DirectoryName!, "RateLimitingOptions.json");
    private static readonly string BannedPropertiesValuesFilePath = IFileSystem.Combine(new FileInfo(typeof(SolutionOperationsCommand).Assembly.Location).DirectoryName!, "BannedPropertiesValues.json");
    
    internal static readonly ConcurrentDictionary<string, Solution> Solutions = new();
    internal static IReadOnlyDictionary<DomainRateLimitingHandler.DomainRateLimitingConfigKey, DomainRateLimitingHandler.DomainRateLimitConfig>? RateLimitingOptions;
    internal static readonly IReadOnlyDictionary<string, HashSet<string>> BannedPropertiesValues = ReadBannedPropertiesValues();
    internal static string DefaultHttpClientCacheDirectoryPath { get; set; } = Path.Combine(Path.GetTempPath(), "DataSourcesCache", "Musoq.DataSources.Roslyn", "NuGet");
    internal static readonly ConcurrentDictionary<string, ReturnCachedResponseHandler> HttpResponseCache = new();
    
    public async Task LoadAsync(string solutionFilePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        logger.LogTrace("Loading solution file: {solutionFilePath}", solutionFilePath);
        
        var workspace = MSBuildWorkspace.Create();
        var solution = await workspace.OpenSolutionAsync(solutionFilePath, cancellationToken: cancellationToken);
        
        logger.LogTrace("Initializing solution");
        
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

        logger.LogTrace("Solution initialized.");
    }
    
    public Task UnloadAsync(string solutionFilePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        logger.LogTrace("Unloading solution file: {solutionFilePath}", solutionFilePath);
        
        Solutions.TryRemove(solutionFilePath, out _);
        
        logger.LogTrace("Solution unloaded.");
            
        return Task.CompletedTask;
    }

    public Task ClearCacheAsync(string cacheDirectoryPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        foreach (var file in Directory.EnumerateFiles(cacheDirectoryPath, "*", SearchOption.AllDirectories))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete cache file: {file}", file);
            }
        }
        
        foreach (var directory in Directory.EnumerateDirectories(cacheDirectoryPath, "*", SearchOption.AllDirectories))
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete cache directory: {directory}", directory);
            }
        }
        
        HttpResponseCache.Clear();
        
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
    
    private static IReadOnlyDictionary<string, HashSet<string>> ReadBannedPropertiesValues()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(BannedPropertiesValuesFilePath);
        
        var bannedPropertiesValues = configuration.Build();
        var propertiesArray = bannedPropertiesValues.GetSection("BannedPropertiesValues").GetChildren();
        var result = new Dictionary<string, HashSet<string>>();
    
        foreach (var property in propertiesArray)
        {
            var propertyName = property.GetValue<string>("propertyName");
            var propertyValue = property.GetValue<string>("propertyValue");

            if (string.IsNullOrEmpty(propertyName) || string.IsNullOrEmpty(propertyValue)) 
                continue;
            
            if (!result.TryGetValue(propertyName, out var value))
            {
                value = [];
                result[propertyName] = value;
            }

            value.Add(propertyValue);
        }
        
        return result;
    }
}
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    private static readonly string RateLimitingOptionsFilePath = IFileSystem.Combine(AppContext.BaseDirectory, "RateLimitingOptions.json");
    
    internal static readonly ConcurrentDictionary<string, Solution> Solutions = new();
    internal static IReadOnlyDictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>? RateLimitingOptions;
    
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
        RateLimitingOptions ??= await ReadDomainRateLimitingOptionsAsync(new Dictionary<string, string>(), cancellationToken);
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
        RateLimitingOptions ??= ReadDomainRateLimitingOptionsAsync(
            new Dictionary<string, string>(), cts.Token).GetAwaiter().GetResult();
    }

    private static Task<IReadOnlyDictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>> ReadDomainRateLimitingOptionsAsync(IReadOnlyDictionary<string, string> environmentVariables, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(RateLimitingOptionsFilePath);
        
        var rateLimitingOptions = configuration.Build();
        var unauthorizedSection = rateLimitingOptions.GetSection("Unauthorized");
        var authorizedSection = rateLimitingOptions.GetSection("Authorized");
        
        var domains = 
            unauthorizedSection.GetChildren().Select(f => f.Key)
                .Concat(authorizedSection.GetChildren().Select(f => f.Key)).ToArray();
        
        var domainRateLimitingOptions = new Dictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>();

        environmentVariables = ExtractAccessTokens(environmentVariables);
        
        foreach (var domain in domains)
        {
            if (environmentVariables.TryGetValue(domain, out _))
            {
                var domainRateLimitingOptionsSection = rateLimitingOptions.GetSection($"Authorized:{domain}");
                
                var permitsPerPeriod = domainRateLimitingOptionsSection.GetValue<int>("PermitsPerPeriod");
                var replenishmentPeriod = domainRateLimitingOptionsSection.GetValue<TimeSpan>("ReplenishmentPeriod");
                var queueLimit = domainRateLimitingOptionsSection.GetValue<int>("QueueLimit");
 
                var domainRateLimitConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(permitsPerPeriod, replenishmentPeriod, queueLimit);
                
                domainRateLimitingOptions[domain] = domainRateLimitConfig;
            }
            else
            {
                var section = $"Unauthorized:{domain}";
                var domainRateLimitingOptionsSection = rateLimitingOptions.GetSection(section);
                
                if (domainRateLimitingOptionsSection.Exists())
                {
                    var permitsPerPeriod = domainRateLimitingOptionsSection.GetValue<int>("PermitsPerPeriod");
                    var replenishmentPeriod = domainRateLimitingOptionsSection.GetValue<TimeSpan>("ReplenishmentPeriod");
                    var queueLimit = domainRateLimitingOptionsSection.GetValue<int>("QueueLimit");
                    
                    var domainRateLimitConfig = new DomainRateLimitingHandler.DomainRateLimitConfig(permitsPerPeriod, replenishmentPeriod, queueLimit);
                    
                    domainRateLimitingOptions[domain] = domainRateLimitConfig;
                }
            }
        }
        
        return Task.FromResult<IReadOnlyDictionary<string, DomainRateLimitingHandler.DomainRateLimitConfig>>(domainRateLimitingOptions);
    }

    private static IReadOnlyDictionary<string, string> ExtractAccessTokens(IReadOnlyDictionary<string, string> environmentVariables)
    {
        var accessTokens = new Dictionary<string, string>();

        foreach (var environmentVariable in environmentVariables)
        {
            if (environmentVariable.Key == "GITHUB_API_KEY") 
                accessTokens.Add("github", environmentVariable.Value);

            if (environmentVariable.Key == "GITLAB_API_KEY")
                accessTokens.Add("gitlab", environmentVariable.Value);
            
            if (environmentVariable.Key == "NUGET_ORG_API_KEY")
                accessTokens.Add("nuget", environmentVariable.Value);
        }

        return accessTokens;
    }
}
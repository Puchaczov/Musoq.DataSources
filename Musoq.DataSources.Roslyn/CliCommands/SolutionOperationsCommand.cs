using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.DataSources.Roslyn.Components.NuGet.Http.Handlers;

namespace Musoq.DataSources.Roslyn.CliCommands;

internal class SolutionOperationsCommand(ILogger logger)
{
    private static readonly object Locker = new();

    // Prefer the plugin directory, but collectible loaders may provide no physical assembly location.
    // The files are optional in that case so command/lifecycle operations remain usable.
    private static readonly string RateLimitingOptionsFilePath = GetPluginFilePath("RateLimitingOptions.json");

    private static readonly string BannedPropertiesValuesFilePath = GetPluginFilePath("BannedPropertiesValues.json");

    internal static readonly ConcurrentDictionary<string, Solution> Solutions = new();

    internal static
        IReadOnlyDictionary<DomainRateLimitingHandler.DomainRateLimitingConfigKey,
            DomainRateLimitingHandler.DomainRateLimitConfig>? RateLimitingOptions;

    internal static readonly IReadOnlyDictionary<string, HashSet<string>> BannedPropertiesValues =
        ReadBannedPropertiesValues();

    internal static readonly ConcurrentDictionary<string, PersistentCacheResponseHandler> HttpResponseCache = new();

    internal static string DefaultCacheDirectoryPath { get; set; } =
        Path.Combine(Path.GetTempPath(), "DataSourcesCache", "Musoq.DataSources.Roslyn");

    internal static ResolveValueStrategy ResolveValueStrategy { get; set; } = ResolveValueStrategy.UseNugetOrgApiOnly;

    public async Task LoadAsync(string solutionFilePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogTrace("Loading solution file: {solutionFilePath}", solutionFilePath);

        if (Solutions.ContainsKey(solutionFilePath))
        {
            logger.LogTrace("Solution already loaded: {solutionFilePath}", solutionFilePath);
            return;
        }

        var solution = await RoslynSolutionLoader.OpenSolutionAsync(solutionFilePath, logger, cancellationToken);

        logger.LogTrace("Initializing solution");

        try
        {
            await Parallel.ForEachAsync(solution.Projects, cancellationToken, async (project, outerToken) =>
            {
                await Parallel.ForEachAsync(project.Documents, outerToken, async (document, innerToken) =>
                {
                    await document.GetSyntaxTreeAsync(innerToken);
                    await document.GetSemanticModelAsync(innerToken);
                });
            });
        }
        catch (MissingMethodException ex)
        {
            throw RoslynVersionHelper.CreateVersionMismatchException(ex,
                "SolutionOperationsCommand.InitializeSolutionAsync");
        }

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
            try
            {
                File.Delete(file);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete cache file: {file}", file);
            }

        foreach (var directory in Directory.EnumerateDirectories(cacheDirectoryPath, "*", SearchOption.AllDirectories))
            try
            {
                Directory.Delete(directory, true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete cache directory: {directory}", directory);
            }

        HttpResponseCache.Clear();

        return Task.CompletedTask;
    }

    public void SetCacheDirectoryPath(string cacheDirectoryPath)
    {
        lock (Locker)
        {
            if (string.IsNullOrEmpty(cacheDirectoryPath))
                throw new ArgumentException("Cache directory path cannot be null or empty.",
                    nameof(cacheDirectoryPath));

            DefaultCacheDirectoryPath = cacheDirectoryPath;

            if (!Directory.Exists(DefaultCacheDirectoryPath))
                Directory.CreateDirectory(DefaultCacheDirectoryPath);
        }
    }

    public string GetCacheDirectoryPath()
    {
        lock (Locker)
        {
            if (string.IsNullOrEmpty(DefaultCacheDirectoryPath))
                throw new InvalidOperationException("Cache directory path is not set.");

            return DefaultCacheDirectoryPath;
        }
    }

    public void SetResolveValueStrategy(string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Resolve value strategy cannot be null or empty.", nameof(value));

        ResolveValueStrategy = Enum.Parse<ResolveValueStrategy>(value, true);
    }

    public string GetResolveValueStrategy()
    {
        if (string.IsNullOrEmpty(ResolveValueStrategy.ToString()))
            throw new InvalidOperationException("Resolve value strategy is not set.");

        return ResolveValueStrategy.ToString();
    }

    public string GetStatus()
    {
        var solutions = Solutions.Keys.OrderBy(path => path, StringComparer.Ordinal).ToArray();
        var builder = new StringBuilder()
            .Append("Loaded solutions: ").AppendLine(solutions.Length.ToString())
            .Append("Cache directory: ").AppendLine(GetCacheDirectoryPath())
            .Append("Resolve value strategy: ").AppendLine(GetResolveValueStrategy());

        if (solutions.Length > 0)
        {
            builder.AppendLine("Solutions:");
            foreach (var solution in solutions)
                builder.Append("  ").AppendLine(solution);
        }

        return builder.ToString().TrimEnd('\r', '\n');
    }

    public static void Initialize()
    {
        using CancellationTokenSource cts = new();
        cts.CancelAfter(TimeSpan.FromMinutes(1));
        RateLimitingOptions ??= ReadDomainRateLimitingOptionsAsync(cts.Token).GetAwaiter().GetResult();
    }

    private static
        Task<IReadOnlyDictionary<DomainRateLimitingHandler.DomainRateLimitingConfigKey,
            DomainRateLimitingHandler.DomainRateLimitConfig>> ReadDomainRateLimitingOptionsAsync(
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(RateLimitingOptionsFilePath, optional: true);

        var rateLimitingOptions = configuration.Build();
        var unauthorizedSection = rateLimitingOptions.GetSection("Unauthorized");
        var authorizedSection = rateLimitingOptions.GetSection("Authorized");

        (string Name, bool WhenApiKeyPresent)[] domains =
            unauthorizedSection.GetChildren().Select(f => (f.Key, false))
                .Concat(authorizedSection.GetChildren().Select(f => (f.Key, true))).ToArray();

        var domainRateLimitingOptions =
            new Dictionary<DomainRateLimitingHandler.DomainRateLimitingConfigKey,
                DomainRateLimitingHandler.DomainRateLimitConfig>();

        foreach (var domain in domains)
        {
            var domainRateLimitConfig = domain.WhenApiKeyPresent
                ? ReadDomainRateLimitConfig("Authorized")
                : ReadDomainRateLimitConfig("Unauthorized");

            if (domainRateLimitConfig != null)
                domainRateLimitingOptions[
                        new DomainRateLimitingHandler.DomainRateLimitingConfigKey(domain.Name,
                            domain.WhenApiKeyPresent)] =
                    domainRateLimitConfig;

            continue;

            DomainRateLimitingHandler.DomainRateLimitConfig? ReadDomainRateLimitConfig(string sectionName)
            {
                var domainRateLimitingOptionsSection = rateLimitingOptions.GetSection($"{sectionName}:{domain.Name}");

                if (!domainRateLimitingOptionsSection.Exists())
                    return null;

                var permitsPerPeriod = domainRateLimitingOptionsSection.GetValue<int>("PermitsPerPeriod");
                var replenishmentPeriod = domainRateLimitingOptionsSection.GetValue<TimeSpan>("ReplenishmentPeriod");
                var queueLimit = domainRateLimitingOptionsSection.GetValue<int>("QueueLimit");

                return new DomainRateLimitingHandler.DomainRateLimitConfig(permitsPerPeriod, replenishmentPeriod,
                    queueLimit);
            }
        }

        return Task
            .FromResult<
                IReadOnlyDictionary<DomainRateLimitingHandler.DomainRateLimitingConfigKey,
                    DomainRateLimitingHandler.DomainRateLimitConfig>>(domainRateLimitingOptions);
    }

    private static IReadOnlyDictionary<string, HashSet<string>> ReadBannedPropertiesValues()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(BannedPropertiesValuesFilePath, optional: true);

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

    private static string GetPluginFilePath(string fileName)
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(SolutionOperationsCommand).Assembly.Location);
        return string.IsNullOrWhiteSpace(assemblyDirectory)
            ? fileName
            : IFileSystem.Combine(assemblyDirectory, fileName);
    }
}

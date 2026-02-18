using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Musoq.DataSources.Roslyn.Components.NuGet.Version;
using Musoq.DataSources.Roslyn.Components.NuGet.Version.Ranges;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal sealed class NuGetPackageMetadataRetriever(
    INuGetCachePathResolver nuGetCachePathResolver,
    string? customApiEndpoint,
    INuGetRetrievalService retrievalService,
    IFileSystem? fileSystem,
    IPackageVersionConcurrencyManager packageVersionConcurrencyManager,
    IReadOnlyDictionary<string, HashSet<string>> bannedPropertiesValues,
    ResolveValueStrategy resolveValueStrategy,
    ILogger logger)
    : INuGetPackageMetadataRetriever
{
    private readonly ConcurrentDictionary<(string, string), List<DependencyInfo>> _packageDependenciesCache = new();

    private readonly ConcurrentDictionary<(string, string), List<Dictionary<string, string?>>> _packageMetadataCache =
        new();

    private IEnumerable<string>? _resolvedPaths;

    public async IAsyncEnumerable<DependencyInfo> GetDependenciesAsync(string packageName, string packageVersion,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(packageVersion))
            yield break;

        packageName = packageName.Trim();
        packageVersion = packageVersion.Trim();

        var lexer = new VersionRangeLexer(packageVersion);
        var parser = new VersionRangeParser(lexer.Tokenize());
        var range = parser.Parse();

        if (range is ExactVersionRange)
        {
            await foreach (var dependencyInfo in InternalProcessPackageVersionAsync(packageName, packageVersion,
                               cancellationToken))
                yield return dependencyInfo;

            yield break;
        }

        var allVersions = await retrievalService.GetPackageVersionsAsync(packageName, cancellationToken);

        foreach (var version in range.ResolveVersions(allVersions))
        await foreach (var dependencyInfo in GetDependenciesAsync(packageName, version, cancellationToken))
            yield return dependencyInfo;
    }

    /// <summary>
    ///     Gets the metadata of the specified NuGet package.
    /// </summary>
    /// <param name="packageName">The name of the NuGet package.</param>
    /// <param name="packageVersion">The version of the NuGet package.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The metadata of the specified NuGet package.</returns>
    public async IAsyncEnumerable<IReadOnlyDictionary<string, string?>> GetMetadataAsync(
        string packageName,
        string packageVersion,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogTrace("Retrieving metadata for {PackageName} {PackageVersion}", packageName, packageVersion);

        packageName = packageName.Trim();
        packageVersion = packageVersion.Trim();

        if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(packageVersion))
        {
            logger.LogError("Package name or version is empty for {PackageName} {PackageVersion}", packageName,
                packageVersion);
            yield break;
        }

        var lexer = new VersionRangeLexer(packageVersion);
        var parser = new VersionRangeParser(lexer.Tokenize());
        var range = parser.Parse();

        if (range is ExactVersionRange)
        {
            await foreach (var metadata in InternalGetMetadataAsync(packageName, packageVersion, cancellationToken))
                yield return metadata;

            yield break;
        }

        var allVersions = await retrievalService.GetPackageVersionsAsync(packageName, cancellationToken);

        foreach (var version in range.ResolveVersions(allVersions))
        await foreach (var metadata in InternalGetMetadataAsync(packageName, version, cancellationToken))
            yield return metadata;
    }

    private async IAsyncEnumerable<DependencyInfo> InternalProcessPackageVersionAsync(string packageName,
        string packageVersion, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_packageDependenciesCache.TryGetValue((packageName, packageVersion), out var cachedDependencies))
        {
            logger.LogTrace("Using cached dependencies for {PackageName} {PackageVersion}", packageName,
                packageVersion);

            foreach (var cachedRow in cachedDependencies) yield return cachedRow;

            yield break;
        }

        Queue<(string PackageName, string Version, uint Level)> packagesToProcess = new();
        var processedPackages = new HashSet<(string, string)>();

        packagesToProcess.Enqueue((packageName, packageVersion, 1));
        processedPackages.Add((packageName, packageVersion));

        do
        {
            var currentPackage = packagesToProcess.Dequeue();

            await foreach (var dependency in InternalProcessXmlDependenciesAsync(currentPackage.PackageName,
                               currentPackage.Version, currentPackage.Level, cancellationToken))
            {
                if (string.IsNullOrEmpty(dependency.PackageId) || string.IsNullOrEmpty(dependency.VersionRange))
                {
                    logger.LogError(
                        "Dependency name or version is empty for {CurrentPackagePackageName} {CurrentPackageVersion}",
                        currentPackage.PackageName, currentPackage.Version);
                    continue;
                }

                var newPackage = dependency.PackageId.Trim();
                var newVersion = dependency.VersionRange.Trim();
                var lexer = new VersionRangeLexer(newVersion);
                var parser = new VersionRangeParser(lexer.Tokenize());
                var range = parser.Parse();

                if (range is ExactVersionRange)
                {
                    if (!processedPackages.Add((newPackage, newVersion)))
                        continue;

                    packagesToProcess.Enqueue((newPackage, newVersion, dependency.Level + 1));

                    yield return dependency;
                    continue;
                }

                var allVersions = await retrievalService.GetPackageVersionsAsync(newPackage, cancellationToken);

                foreach (var version in range.ResolveVersions(allVersions))
                {
                    if (!processedPackages.Add((newPackage, version)))
                        continue;

                    packagesToProcess.Enqueue((newPackage, version, dependency.Level + 1));

                    yield return new DependencyInfo(newPackage, version, dependency.TargetFramework,
                        dependency.Level + 1);
                }
            }
        } while (!cancellationToken.IsCancellationRequested && packagesToProcess.Count > 0);
    }

    private async IAsyncEnumerable<DependencyInfo> InternalProcessXmlDependenciesAsync(string packageName,
        string packageVersion, uint level, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogTrace("Retrieving metadata for {PackageName} {PackageVersion}", packageName, packageVersion);

        if (packageVersion.Length == 0)
        {
            logger.LogError("Package version is empty for {PackageName}", packageName);
            yield break;
        }

        if (packageVersion[0] == '[' && packageVersion[^1] == ']') packageVersion = packageVersion[1..^1];

        if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(packageVersion))
        {
            logger.LogError("Package name or version is empty for {PackageName} {PackageVersion}", packageName,
                packageVersion);
            yield break;
        }

        if (_packageDependenciesCache.TryGetValue((packageName, packageVersion), out var cachedDependencies))
        {
            logger.LogTrace("Using cached dependencies for {PackageName} {PackageVersion}", packageName,
                packageVersion);

            foreach (var cachedRow in cachedDependencies) yield return cachedRow;

            yield break;
        }

        using var @lock =
            await packageVersionConcurrencyManager.AcquireLockAsync(packageName, packageVersion, cancellationToken);

        if (_packageDependenciesCache.TryGetValue((packageName, packageVersion), out cachedDependencies))
        {
            logger.LogTrace("Using cached dependencies for {PackageName} {PackageVersion}", packageName,
                packageVersion);

            foreach (var cachedRow in cachedDependencies) yield return cachedRow;

            yield break;
        }

        var packagePath = GetPackageCachePath(nuGetCachePathResolver, packageName, packageVersion) ??
                          await TryDownloadPackageAsync(packageName, packageVersion, cancellationToken);

        if (string.IsNullOrWhiteSpace(packagePath))
        {
            logger.LogError("Package path is empty for {PackageName} {PackageVersion}", packageName, packageVersion);
            yield break;
        }

        var nuspecPath = IFileSystem.Combine(packagePath, $"{packageName}.nuspec");

        if (!fileSystem.IsFileExists(nuspecPath))
        {
            logger.LogError("Nuspec file not found for {PackageName} {PackageVersion}", packageName, packageVersion);
            yield break;
        }

        await using var file = fileSystem.OpenRead(nuspecPath);

        var xml = XDocument.Load(file);
        var ns = xml.Root?.GetDefaultNamespace() ?? XNamespace.None;

        var ungroupedDependencies = xml.Root
            ?.Element(ns + "metadata")
            ?.Element(ns + "dependencies")
            ?.Elements(ns + "dependency")
            .Select(x => new DependencyInfo(
                x.Attribute("id")?.Value ?? string.Empty,
                x.Attribute("version")?.Value ?? string.Empty,
                string.Empty,
                level)) ?? [];

        var groupedDependencies = xml.Root
            ?.Element(ns + "metadata")
            ?.Element(ns + "dependencies")
            ?.Elements(ns + "group")
            .SelectMany(g => g.Elements(ns + "dependency")
                .Select(x => new DependencyInfo(
                    x.Attribute("id")?.Value ?? string.Empty,
                    x.Attribute("version")?.Value ?? string.Empty,
                    g.Attribute("targetFramework")?.Value ?? string.Empty,
                    level))) ?? [];

        var dependencies = new List<DependencyInfo>();

        foreach (var dependency in ungroupedDependencies.Concat(groupedDependencies))
        {
            if (string.IsNullOrEmpty(dependency.PackageId) || string.IsNullOrEmpty(dependency.VersionRange))
            {
                logger.LogError("Dependency name or version is empty for {PackageName} {PackageVersion}", packageName,
                    packageVersion);
                continue;
            }

            dependencies.Add(dependency);

            yield return dependency;
        }

        _packageDependenciesCache.AddOrUpdate((packageName, packageVersion),
            dependencies,
            (_, _) => dependencies);
    }

    private async IAsyncEnumerable<IReadOnlyDictionary<string, string?>> InternalGetMetadataAsync(string packageName,
        string packageVersion, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        packageName = packageName.Trim();

        if (_packageMetadataCache.TryGetValue((packageName, packageVersion), out var cachedMetadata))
        {
            logger.LogTrace("Using cached metadata for {PackageName} {PackageVersion}", packageName, packageVersion);

            foreach (var cachedRow in cachedMetadata) yield return cachedRow;

            yield break;
        }

        using var @lock =
            await packageVersionConcurrencyManager.AcquireLockAsync(packageName, packageVersion, cancellationToken);

        if (_packageMetadataCache.TryGetValue((packageName, packageVersion), out cachedMetadata))
        {
            logger.LogTrace("Using cached metadata for {PackageName} {PackageVersion}", packageName, packageVersion);

            foreach (var cachedRow in cachedMetadata) yield return cachedRow;

            yield break;
        }

        var commonResources = new NuGetResource
        {
            PackagePath = GetPackageCachePath(nuGetCachePathResolver, packageName, packageVersion) ??
                          await TryDownloadPackageAsync(packageName, packageVersion, cancellationToken),
            PackageName = packageName,
            PackageVersion = packageVersion
        };

        var retrieveCommonResourcesVisitor = new NuGetResourceVisitor(
            commonResources,
            retrievalService,
            customApiEndpoint,
            bannedPropertiesValues,
            resolveValueStrategy,
            logger);

        await commonResources.AcceptAsync(retrieveCommonResourcesVisitor, cancellationToken);

        var licenses = commonResources.Licenses;
        var metadata = new List<Dictionary<string, string?>>();

        Dictionary<string, string?> row;

        if (licenses.Length == 0)
        {
            logger.LogTrace("No licenses found for {PackageName} {PackageVersion}", packageName, packageVersion);

            row = BuildMetadata(null, commonResources);

            metadata.Add(row);

            yield return row;
        }
        else
        {
            logger.LogTrace("Found {LicensesLength} licenses for {PackageName} {PackageVersion}", licenses.Length,
                packageName, packageVersion);

            row = BuildMetadata(licenses[0], commonResources);

            metadata.Add(row);

            yield return row;

            for (var i = 1; i < licenses.Length; i++)
            {
                row = BuildMetadata(licenses[i], commonResources);

                metadata.Add(row);

                yield return row;
            }
        }

        _packageMetadataCache.AddOrUpdate((packageName, packageVersion),
            metadata,
            (_, _) => metadata);
    }

    private string? GetPackageCachePath(INuGetCachePathResolver resolver, string packageName, string packageVersion)
    {
        _resolvedPaths ??= resolver.ResolveAll();

        var packagePath = _resolvedPaths
            .Select(cache => Path.Combine(cache, packageName, packageVersion))
            .FirstOrDefault(fileSystem.IsDirectoryExists);

        logger.LogTrace("Package cache used: {PackagePath} for {PackageName} {PackageVersion}", packagePath,
            packageName, packageVersion);

        return packagePath;
    }

    private async Task<string?> TryDownloadPackageAsync(string packageName, string packageVersion,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(packageVersion))
        {
            logger.LogError("Package name or version is empty for {PackageName} {PackageVersion}", packageName,
                packageVersion);

            return null;
        }

        var tempPath = Path.GetTempPath();
        var packagePath = Path.Combine(tempPath, "NuGetPackages", packageName, packageVersion);

        if (fileSystem.IsDirectoryExists(packagePath))
            return packagePath;

        try
        {
            logger.LogTrace("Package downloader used: {PackagePath} for {PackageName} {PackageVersion}", packagePath,
                packageName, packageVersion);

            return await retrievalService.DownloadPackageAsync(packageName, packageVersion, packagePath,
                cancellationToken);
        }
        catch (Exception exc)
        {
            logger.LogError(exc, "Failed to download package {PackageName} {PackageVersion}", packageName,
                packageVersion);

            return null;
        }
    }

    private static Dictionary<string, string?> BuildMetadata(NuGetLicense? license, NuGetResource? commonResources)
    {
        return new Dictionary<string, string?>
        {
            [nameof(NuGetLicense.LicenseUrl)] = license?.LicenseUrl,
            [nameof(NuGetLicense.LicenseContent)] = license?.LicenseContent,
            [nameof(NuGetLicense.License)] = license?.License,
            [nameof(NuGetResource.ProjectUrl)] = commonResources?.ProjectUrl,
            [nameof(NuGetResource.Title)] = commonResources?.Title,
            [nameof(NuGetResource.Authors)] = commonResources?.Authors,
            [nameof(NuGetResource.Owners)] = commonResources?.Owners,
            [nameof(NuGetResource.RequireLicenseAcceptance)] = commonResources?.RequireLicenseAcceptance?.ToString(),
            [nameof(NuGetResource.Description)] = commonResources?.Description,
            [nameof(NuGetResource.Summary)] = commonResources?.Summary,
            [nameof(NuGetResource.ReleaseNotes)] = commonResources?.ReleaseNotes,
            [nameof(NuGetResource.Copyright)] = commonResources?.Copyright,
            [nameof(NuGetResource.Language)] = commonResources?.Language,
            [nameof(NuGetResource.Tags)] = commonResources?.Tags
        };
    }
}
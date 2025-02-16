using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal class NuGetCachePathResolver(string? solutionPath) : INuGetCachePathResolver
{
    public IEnumerable<string> ResolveAll()
    {
        var cache = new HashSet<string>();
            
        var configPaths = new[]
        {
            // Solution-level config if solution path is provided
            solutionPath != null ? Path.Combine(new FileInfo(solutionPath).DirectoryName!, "nuget.config") : null,
            // User-level config
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "NuGet.Config"),
            // Machine-wide config
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NuGet", "NuGet.Config")
        }.Where(p => p is not null && File.Exists(p));

        foreach (var configPath in configPaths)
        {
            var doc = XDocument.Load(configPath!);
            var config = doc.Root;

            // Check for repository path in config section
            var repositoryPath = config
                ?.Element("config")
                ?.Elements("add")
                .FirstOrDefault(e => e.Attribute("key")?.Value == "repositoryPath")
                ?.Attribute("value")?.Value;

            if (!string.IsNullOrEmpty(repositoryPath))
            {
                // Handle relative paths
                if (!Path.IsPathRooted(repositoryPath))
                {
                    repositoryPath = Path.GetFullPath(Path.Combine(
                        Path.GetDirectoryName(configPath)!,
                        repositoryPath));
                }

                cache.Add(repositoryPath);
            }

            // Check for global packages folder
            var globalPackagesFolder = config
                ?.Element("config")
                ?.Elements("add")
                .FirstOrDefault(e => e.Attribute("key")?.Value == "globalPackagesFolder")
                ?.Attribute("value")?.Value;

            if (string.IsNullOrEmpty(globalPackagesFolder)) continue;
                
            // Handle relative paths
            if (!Path.IsPathRooted(globalPackagesFolder))
            {
                globalPackagesFolder = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(configPath)!,
                    globalPackagesFolder));
            }

            cache.Add(globalPackagesFolder);
        }

        if (solutionPath != null)
        {
            var localPackages = Path.Combine(Path.GetDirectoryName(solutionPath)!, "packages");
            if (Directory.Exists(localPackages))
                cache.Add(localPackages);
        }

        var localCache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NuGet",
            "Cache");
        if (Directory.Exists(localCache))
            cache.Add(localCache);

        var v3Cache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NuGet",
            "v3-cache");
        if (Directory.Exists(v3Cache))
            cache.Add(v3Cache);

        // Add default global packages location if no custom locations found
        if (cache.Count == 0)
        {
            cache.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget",
                "packages"));
        }

        return cache;
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal class NuGetCachePathResolver(string? solutionPath, OSPlatform osPlatform) : INuGetCachePathResolver
{
    private static readonly string UserProfileFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    
    private static readonly string CommonApplicationDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    private static readonly string LocalApplicationDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    
    public IEnumerable<string> ResolveAll()
    {
        var cache = new HashSet<string>();

        AddEnvGlobalPackages(cache);

        var configPaths = CollectNuGetConfigPaths();
        ProcessNuGetConfigs(configPaths, cache);

        AddSolutionPackages(cache);
        AddHttpCache(cache);
        AddPluginsCache(cache);
        AddDefaultGlobalPackages(cache);

        return cache;
    }

    private static void AddEnvGlobalPackages(HashSet<string> cache)
    {
        var envPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrEmpty(envPackages))
            cache.Add(Path.GetFullPath(envPackages));
    }

    private IEnumerable<string> CollectNuGetConfigPaths()
    {
        IEnumerable<string> configPaths = new List<string?>
        {
            solutionPath != null ? Path.Combine(new FileInfo(solutionPath).DirectoryName!, "nuget.config") : null,
            Path.Combine(UserProfileFolder, ".nuget", "NuGet.Config"),
            Path.Combine(CommonApplicationDataFolder, "NuGet", "NuGet.Config")
        }
        .Where(p => p is not null)!;

        if (osPlatform.Equals(OSPlatform.Windows)) return configPaths;
        
        var home = Environment.GetEnvironmentVariable("HOME");
        
        if (string.IsNullOrEmpty(home)) return configPaths;
        
        var linuxConfig = Path.Combine(home, ".nuget", "NuGet.Config");
        configPaths = configPaths.Append(linuxConfig).ToList();
        
        return configPaths;
    }

    private static void ProcessNuGetConfigs(IEnumerable<string> configPaths, HashSet<string> cache)
    {
        foreach (var configPath in configPaths)
        {
            try
            {
                if (!IFileSystem.FileExists(configPath))
                    continue;
                
                var doc = XDocument.Load(configPath);
                var config = doc.Root;

                var repositoryPath = config
                    ?.Element("config")
                    ?.Elements("add")
                    .FirstOrDefault(e => e.Attribute("key")?.Value == "repositoryPath")
                    ?.Attribute("value")?.Value;

                if (!string.IsNullOrEmpty(repositoryPath))
                {
                    if (!Path.IsPathRooted(repositoryPath))
                        repositoryPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(configPath)!, repositoryPath));
                    cache.Add(repositoryPath);
                }

                var globalPackagesFolder = config
                    ?.Element("config")
                    ?.Elements("add")
                    .FirstOrDefault(e => e.Attribute("key")?.Value == "globalPackagesFolder")
                    ?.Attribute("value")?.Value;

                if (string.IsNullOrEmpty(globalPackagesFolder)) 
                    continue;
                
                if (!Path.IsPathRooted(globalPackagesFolder))
                    globalPackagesFolder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(configPath)!, globalPackagesFolder));
                
                cache.Add(globalPackagesFolder);
            }
            catch { /* Ignore any parsing errors */ }
        }
    }

    private void AddSolutionPackages(HashSet<string> cache)
    {
        if (solutionPath != null)
            cache.Add(Path.Combine(Path.GetDirectoryName(solutionPath)!, "packages"));
    }

    private void AddHttpCache(HashSet<string> cache)
    {
        var envHttpCache = Environment.GetEnvironmentVariable("NUGET_HTTP_CACHE_PATH");
        if (!string.IsNullOrEmpty(envHttpCache))
            cache.Add(Path.GetFullPath(envHttpCache));
        else
        {
            if (osPlatform.Equals(OSPlatform.Windows))
            {
                var winV3 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NuGet", "v3-cache");
                cache.Add(winV3);
            }
            else
            {
                var home = Environment.GetEnvironmentVariable("HOME") ?? "~";
                var unixV3 = Path.Combine(home, ".local", "share", "NuGet", "v3-cache");
                cache.Add(Path.GetFullPath(unixV3));
            }
        }
    }

    private void AddPluginsCache(HashSet<string> cache)
    {
        var envPluginsCache = Environment.GetEnvironmentVariable("NUGET_PLUGINS_CACHE_PATH");
        if (!string.IsNullOrEmpty(envPluginsCache))
            cache.Add(Path.GetFullPath(envPluginsCache));
        else
        {
            if (osPlatform.Equals(OSPlatform.Windows))
            {
                var winPlugins = Path.Combine(LocalApplicationDataFolder, "NuGet", "plugins-cache");
                cache.Add(winPlugins);
            }
            else
            {
                var home = Environment.GetEnvironmentVariable("HOME") ?? "~";
                var unixPlugins = Path.Combine(home, ".local", "share", "NuGet", "plugins-cache");
                cache.Add(Path.GetFullPath(unixPlugins));
            }
        }
    }

    private static void AddDefaultGlobalPackages(HashSet<string> cache)
    {
        cache.Add(Path.Combine(UserProfileFolder, ".nuget", "packages"));
    }
}
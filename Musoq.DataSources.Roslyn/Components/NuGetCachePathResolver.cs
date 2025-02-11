using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Musoq.DataSources.Roslyn.Components
{
    internal class NuGetCachePathResolver : INuGetCachePathResolver
    {
        public string Resolve()
        {
            var nuGetPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES");

            if (!string.IsNullOrEmpty(nuGetPath))
            {
                return nuGetPath;
            }

            var userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !string.IsNullOrEmpty(userProfile))
            {
                return Path.Combine(userProfile, ".nuget", "packages");
            }

            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                return Path.Combine(home, ".nuget", "packages");
            }

            throw new InvalidOperationException("Could not resolve NuGet cache path. Please set the NUGET_PACKAGES environment variable.");
        }
    }
}

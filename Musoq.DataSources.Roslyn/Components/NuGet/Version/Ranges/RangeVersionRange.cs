using System.Collections.Generic;
using System.Linq;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Version.Ranges;

// Represents a version range (e.g., "[1.0.0, 2.0.0)")
internal class RangeVersionRange(string? minVersion, bool inclusiveMin, string? maxVersion, bool inclusiveMax)
    : VersionRange
{
    public override IEnumerable<string> ResolveVersions(IEnumerable<string> availableVersions)
    {
        return availableVersions.Where(v => 
        {
            var matchesMin = true;
            var matchesMax = true;
            
            if (minVersion != null)
            {
                var comparison = CompareVersions(v, minVersion);
                matchesMin = inclusiveMin ? comparison >= 0 : comparison > 0;
            }
            
            if (maxVersion != null)
            {
                var comparison = CompareVersions(v, maxVersion);
                matchesMax = inclusiveMax ? comparison <= 0 : comparison < 0;
            }
            
            return matchesMin && matchesMax;
        });
    }
    
    private static int CompareVersions(string a, string b)
    {
        // Split version into components and prerelease parts
        var aParts = SplitVersion(a);
        var bParts = SplitVersion(b);
        
        // Compare numeric version parts (major.minor.patch...)
        var maxLength = System.Math.Max(aParts.NumericParts.Length, bParts.NumericParts.Length);
        for (var i = 0; i < maxLength; i++)
        {
            // Get component values, treating missing components as 0
            var aValue = i < aParts.NumericParts.Length ? aParts.NumericParts[i] : 0;
            var bValue = i < bParts.NumericParts.Length ? bParts.NumericParts[i] : 0;
            
            if (aValue < bValue)
                return -1;
                
            if (aValue > bValue)
                return 1;
        }
        
        // Check if one version has more parts than the other
        // This happens when comparing e.g. "1.0.0" with "1.0.0.0"
        // We consider the version with more parts to be greater, even if additional parts are zeros
        if (aParts.NumericParts.Length < bParts.NumericParts.Length)
            return -1; // b has more parts, so b is greater
        if (aParts.NumericParts.Length > bParts.NumericParts.Length)
            return 1;  // a has more parts, so a is greater

        // If we get here, the numeric parts are equivalent, so check prerelease parts
        
        // If one has prerelease and the other doesn't, the one without prerelease is greater
        if (string.IsNullOrEmpty(aParts.PreRelease) && !string.IsNullOrEmpty(bParts.PreRelease))
            return 1;
            
        if (!string.IsNullOrEmpty(aParts.PreRelease) && string.IsNullOrEmpty(bParts.PreRelease))
            return -1;
            
        // Both have prerelease or both don't have prerelease
        // If neither has prerelease, they're equal
        if (string.IsNullOrEmpty(aParts.PreRelease))
            return 0;
            
        // Compare prerelease tags lexically
        return string.Compare(aParts.PreRelease, bParts.PreRelease, System.StringComparison.OrdinalIgnoreCase);
    }
    
    private static VersionParts SplitVersion(string version)
    {
        // Split version into numeric part and prerelease part (if any)
        var dashIndex = version.IndexOf('-');
        
        var numericPart = dashIndex > 0 ? version.Substring(0, dashIndex) : version;
        var preReleasePart = dashIndex > 0 ? version.Substring(dashIndex + 1) : "";
        
        // Split numeric part into components and parse them
        var parts = numericPart.Split('.');
        var numericComponents = new long[parts.Length];
        
        for (var i = 0; i < parts.Length; i++)
        {
            // Try to parse as long, if it fails due to overflow or format, use 0
            if (!long.TryParse(parts[i], out var num))
            {
                // For non-numeric or too large values, treat as 0
                num = 0;
            }
            numericComponents[i] = num;
        }
            
        return new VersionParts(numericComponents, preReleasePart);
    }
    
    private class VersionParts(long[] numericParts, string preRelease)
    {
        public long[] NumericParts { get; } = numericParts;
        public string PreRelease { get; } = preRelease;
    }
}

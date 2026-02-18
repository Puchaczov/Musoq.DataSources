using System;
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
        var aParts = SplitVersion(a);
        var bParts = SplitVersion(b);


        var maxLength = Math.Max(aParts.NumericParts.Length, bParts.NumericParts.Length);
        for (var i = 0; i < maxLength; i++)
        {
            var aValue = i < aParts.NumericParts.Length ? aParts.NumericParts[i] : 0;
            var bValue = i < bParts.NumericParts.Length ? bParts.NumericParts[i] : 0;

            if (aValue < bValue)
                return -1;

            if (aValue > bValue)
                return 1;
        }


        if (aParts.NumericParts.Length < bParts.NumericParts.Length)
            return -1;
        if (aParts.NumericParts.Length > bParts.NumericParts.Length)
            return 1;


        if (string.IsNullOrEmpty(aParts.PreRelease) && !string.IsNullOrEmpty(bParts.PreRelease))
            return 1;

        if (!string.IsNullOrEmpty(aParts.PreRelease) && string.IsNullOrEmpty(bParts.PreRelease))
            return -1;


        if (string.IsNullOrEmpty(aParts.PreRelease))
            return 0;


        return string.Compare(aParts.PreRelease, bParts.PreRelease, StringComparison.OrdinalIgnoreCase);
    }

    private static VersionParts SplitVersion(string version)
    {
        var dashIndex = version.IndexOf('-');

        var numericPart = dashIndex > 0 ? version.Substring(0, dashIndex) : version;
        var preReleasePart = dashIndex > 0 ? version.Substring(dashIndex + 1) : "";


        var parts = numericPart.Split('.');
        var numericComponents = new long[parts.Length];

        for (var i = 0; i < parts.Length; i++)
        {
            if (!long.TryParse(parts[i], out var num)) num = 0;
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
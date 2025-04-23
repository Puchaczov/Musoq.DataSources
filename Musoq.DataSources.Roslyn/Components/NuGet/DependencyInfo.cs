using System.Diagnostics;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

/// <summary>
/// Represents a dependency information.
/// </summary>
/// <param name="packageId">The package ID of the dependency.</param>
/// <param name="versionRange">The version range of the dependency.</param>
/// <param name="targetFramework">The target framework of the dependency.</param>
/// <param name="level">The level of transitivity of the dependency.</param>
[DebuggerDisplay("{PackageId}, {VersionRange} -> {Level})")]
public class DependencyInfo(string packageId, string versionRange, string? targetFramework, uint level)
{
    /// <summary>
    /// Gets the package ID of the dependency.
    /// </summary>
    public string PackageId { get; } = packageId;

    /// <summary>
    /// Gets the version range of the dependency.
    /// </summary>
    public string VersionRange { get; } = versionRange;

    /// <summary>
    /// Gets the target framework of the dependency.
    /// </summary>
    public string? TargetFramework { get; } = targetFramework;
    
    /// <summary>
    /// Gets the level of transitivity of the dependency.
    /// </summary>
    public uint Level { get; } = level;
}
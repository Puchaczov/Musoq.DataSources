using System;
using Microsoft.CodeAnalysis;

namespace Musoq.DataSources.Roslyn;

/// <summary>
///     Helper class to diagnose Microsoft.CodeAnalysis version conflicts.
/// </summary>
internal static class RoslynVersionHelper
{
    private const string ExpectedVersion = "4.14.0";

    /// <summary>
    ///     Wraps a MissingMethodException with detailed version information.
    /// </summary>
    /// <param name="ex">The original exception.</param>
    /// <param name="context">Additional context about where the error occurred.</param>
    /// <returns>An InvalidOperationException with detailed diagnostics.</returns>
    public static InvalidOperationException CreateVersionMismatchException(MissingMethodException ex,
        string context = "")
    {
        var workspacesAssembly = typeof(Document).Assembly;
        var loadedVersion = workspacesAssembly.GetName().Version;
        var assemblyLocation = workspacesAssembly.Location;

        var contextInfo = string.IsNullOrEmpty(context) ? "" : $" Context: {context}.";

        return new InvalidOperationException(
            $"Microsoft.CodeAnalysis version mismatch detected.{contextInfo} " +
            $"Expected version: {ExpectedVersion}, " +
            $"Loaded version: {loadedVersion}, " +
            $"Assembly location: {assemblyLocation}. " +
            $"The Roslyn API method signature is incompatible. " +
            $"This typically occurs when the host application loads a different version of Microsoft.CodeAnalysis " +
            $"than the Roslyn plugin was compiled against. " +
            $"To fix: ensure the host application uses Microsoft.CodeAnalysis {ExpectedVersion} or remove 'ExcludeAssets' from the plugin's PackageReferences.",
            ex);
    }

    /// <summary>
    ///     Gets diagnostic information about the currently loaded Microsoft.CodeAnalysis version.
    /// </summary>
    /// <returns>A string with version diagnostics.</returns>
    public static string GetVersionDiagnostics()
    {
        var workspacesAssembly = typeof(Document).Assembly;
        var loadedVersion = workspacesAssembly.GetName().Version;
        var assemblyLocation = workspacesAssembly.Location;

        return
            $"Microsoft.CodeAnalysis version: {loadedVersion} (Expected: {ExpectedVersion}), Location: {assemblyLocation}";
    }
}
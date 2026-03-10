using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Musoq.DataSources.Roslyn.Tests;

internal static class RoslynTestSolutionLocator
{
    private static readonly ConcurrentDictionary<string, string> CachedSolutionPaths = new();

    /// <summary>
    ///     Paths shorter than this are safe for MSBuild/Roslyn on Windows.
    ///     Staging to a temp directory is only needed when the path is long
    ///     enough to risk hitting the 260-char MAX_PATH limit.
    /// </summary>
    private const int SafePathLengthThreshold = 150;

    public static string GetSolutionPath<TAnchor>(string solutionName)
    {
        var sourceSolutionPath = ResolveSourceSolutionPath(typeof(TAnchor), solutionName);

        if (!OperatingSystem.IsWindows())
            return sourceSolutionPath;

        if (sourceSolutionPath.Length <= SafePathLengthThreshold)
            return sourceSolutionPath;

        return CachedSolutionPaths.GetOrAdd(sourceSolutionPath, StageSolutionToShortPath);
    }

    private static string ResolveSourceSolutionPath(Type anchorType, string solutionName)
    {
        var assemblyDirectory = Path.GetDirectoryName(anchorType.Assembly.Location);

        if (string.IsNullOrEmpty(assemblyDirectory))
            throw new InvalidOperationException("Directory is empty.");

        var repositoryRoot = FindRepositoryRoot(assemblyDirectory);

        if (repositoryRoot != null)
        {
            var sourceSolutionPath = Path.Combine(repositoryRoot, "Musoq.DataSources.Roslyn.Tests", "TestsSolutions",
                solutionName, $"{solutionName}.sln");

            if (File.Exists(sourceSolutionPath))
                return sourceSolutionPath;
        }

        var outputSolutionPath = Path.Combine(assemblyDirectory, "TestsSolutions", solutionName, $"{solutionName}.sln");

        if (File.Exists(outputSolutionPath))
            return outputSolutionPath;

        throw new FileNotFoundException($"Could not locate solution '{solutionName}'.", outputSolutionPath);
    }

    private static string? FindRepositoryRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);

        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Musoq.DataSources.sln")))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }

    private static string StageSolutionToShortPath(string sourceSolutionPath)
    {
        var sourceSolutionDirectory = Path.GetDirectoryName(sourceSolutionPath);

        if (string.IsNullOrEmpty(sourceSolutionDirectory))
            throw new InvalidOperationException($"Could not resolve directory for '{sourceSolutionPath}'.");

        var solutionName = Path.GetFileNameWithoutExtension(sourceSolutionPath);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceSolutionDirectory)))[..8];
        var stagedRoot = Path.Combine(Path.GetTempPath(), "mrds", $"{solutionName}_{Process.GetCurrentProcess().Id}_{hash}");

        if (Directory.Exists(stagedRoot))
            Directory.Delete(stagedRoot, true);

        CopyDirectory(sourceSolutionDirectory, stagedRoot);

        var stagedSolutionPath = Path.Combine(stagedRoot, Path.GetFileName(sourceSolutionPath));

        if (!File.Exists(stagedSolutionPath))
            throw new FileNotFoundException($"Staged solution was not copied correctly: '{stagedSolutionPath}'.");

        RestoreSolution(stagedSolutionPath);

        return stagedSolutionPath;
    }

    private static void RestoreSolution(string solutionPath)
    {
        var psi = new ProcessStartInfo("dotnet", $"restore \"{solutionPath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(solutionPath) ?? string.Empty
        };

        using var process = Process.Start(psi);

        if (process is null)
            throw new InvalidOperationException("Failed to start dotnet restore.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(TimeSpan.FromMinutes(2)))
            throw new InvalidOperationException(
                $"dotnet restore timed out after 2 minutes for '{solutionPath}'.");

        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"dotnet restore failed (exit code {process.ExitCode}) for '{solutionPath}'.{Environment.NewLine}" +
                $"stdout: {stdout}{Environment.NewLine}" +
                $"stderr: {stderr}");
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var file in Directory.GetFiles(sourceDirectory))
        {
            var destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(file));
            File.Copy(file, destinationFile, true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDirectory))
        {
            var directoryName = Path.GetFileName(directory);

            if (directoryName is "bin" or "obj" or ".idea" or ".vs")
                continue;

            CopyDirectory(directory, Path.Combine(destinationDirectory, directoryName));
        }
    }
}

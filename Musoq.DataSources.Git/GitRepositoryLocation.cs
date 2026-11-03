using System;
using System.IO;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.Git;

/// <summary>Separates private repository metadata, shared objects, and the selected checkout.</summary>
internal sealed record GitRepositoryLocation(
    string GitDirectory, string? WorktreeRoot, string CommonDirectory)
{
    public string ExecutionRoot => WorktreeRoot ?? GitDirectory;

    public static GitRepositoryLocation Resolve(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var requested = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!Directory.Exists(requested) && !File.Exists(requested))
            throw new InvalidOperationException($"The path '{path}' is not a directory",
                GitSchema.Reports.Failure(DataSourceFailureKind.ResourceNotFound));
        string gitDirectory;
        string? worktree;
        var gitEntry = Path.Combine(requested, ".git");
        var rootHasGitDirectory = Directory.Exists(gitEntry);
        if (rootHasGitDirectory)
        {
            gitDirectory = gitEntry;
            worktree = requested;
        }
        else if (File.Exists(gitEntry) || File.Exists(requested))
        {
            var pointer = File.Exists(gitEntry) ? gitEntry : requested;
            var text = File.ReadAllText(pointer).TrimEnd('\r', '\n');
            if (!text.StartsWith("gitdir:", StringComparison.Ordinal))
                throw new InvalidOperationException($"The path '{path}' contains an invalid Git pointer file.",
                    GitSchema.Reports.Failure(DataSourceFailureKind.InvalidInput));
            gitDirectory = Path.GetFullPath(text[7..].TrimStart(), Path.GetDirectoryName(pointer)!);
            worktree = Path.GetDirectoryName(pointer);
        }
        else
        {
            gitDirectory = requested;
            var backPointer = Path.Combine(gitDirectory, "gitdir");
            worktree = File.Exists(backPointer)
                ? Path.GetDirectoryName(Path.GetFullPath(File.ReadAllText(backPointer).TrimEnd('\r', '\n'), gitDirectory))
                : Path.GetFileName(gitDirectory).Equals(".git", OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ? Path.GetDirectoryName(gitDirectory) : null;
            if (!File.Exists(backPointer) && worktree is not null)
            {
                using var config = LibGit2Sharp.Configuration.BuildFrom(Path.Combine(gitDirectory, "config"));
                if (config.Get<bool>("core.bare")?.Value == true) worktree = null;
            }
        }
        var commonFile = Path.Combine(gitDirectory, "commondir");
        var commonDirectory = File.Exists(commonFile)
            ? Path.GetFullPath(File.ReadAllText(commonFile).TrimEnd('\r', '\n'), gitDirectory)
            : gitDirectory;
        // A root's .git entry establishes its identity. The actual backend operation validates its contents.
        // Direct metadata paths and gitfiles still need a shape check to reject unrelated paths/broken pointers.
        if (!rootHasGitDirectory && !File.Exists(Path.Combine(gitDirectory, "HEAD")))
            throw new InvalidOperationException($"The path '{path}' does not contain a Git repository",
                GitSchema.Reports.Failure(DataSourceFailureKind.ResourceNotFound));
        return new GitRepositoryLocation(gitDirectory, worktree, commonDirectory);
    }
}

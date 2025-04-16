using Musoq.DataSources.Roslyn.Components;
using System.IO;

namespace Musoq.DataSources.Roslyn.Tests.Components;

/// <summary>
/// Helper class to coordinate operations between MockFileSystem and MockFileWatcher
/// </summary>
public static class MockFileSystemWatcherIntegration
{
    /// <summary>
    /// Creates a file in the mock file system and triggers the appropriate watcher event
    /// </summary>
    public static void SimulateExternalFileCreation(MockFileSystem fileSystem, MockFileWatcher fileWatcher, string path, string content)
    {
        fileSystem.SimulateFileCreated(path, content);
        fileWatcher.SimulateFileCreated(path);
    }

    /// <summary>
    /// Deletes a file from the mock file system and triggers the appropriate watcher event
    /// </summary>
    public static void SimulateExternalFileDeletion(MockFileSystem fileSystem, MockFileWatcher fileWatcher, string path)
    {
        fileSystem.SimulateFileDeleted(path);
        fileWatcher.SimulateFileDeleted(path);
    }

    /// <summary>
    /// Renames a file in the mock file system and triggers the appropriate watcher event
    /// </summary>
    public static void SimulateExternalFileRename(MockFileSystem fileSystem, MockFileWatcher fileWatcher, string oldPath, string newPath)
    {
        // Get content from old file if it exists
        string? content = null;
        try
        {
            content = fileSystem.ReadAllTextAsync(oldPath, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (FileNotFoundException)
        {
            // If file doesn't exist, just create an empty one
            content = string.Empty;
        }
        
        // Create new file with the content
        fileSystem.SimulateFileCreated(newPath, content);
        
        // Delete the old file
        fileSystem.SimulateFileDeleted(oldPath);
        
        // Trigger the rename event
        fileWatcher.SimulateFileRenamed(oldPath, newPath);
    }
}

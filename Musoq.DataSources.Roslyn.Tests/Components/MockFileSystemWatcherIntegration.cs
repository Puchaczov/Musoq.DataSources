namespace Musoq.DataSources.Roslyn.Tests.Components;

/// <summary>
///     Helper class to coordinate operations between MockFileSystem and MockFileWatcher
/// </summary>
public static class MockFileSystemWatcherIntegration
{
    /// <summary>
    ///     Creates a file in the mock file system and triggers the appropriate watcher event
    /// </summary>
    public static void SimulateExternalFileCreation(MockFileSystem fileSystem, MockFileWatcher fileWatcher, string path,
        string content)
    {
        fileSystem.SimulateFileCreated(path, content);
        fileWatcher.SimulateFileCreated(path);
    }

    /// <summary>
    ///     Deletes a file from the mock file system and triggers the appropriate watcher event
    /// </summary>
    public static void SimulateExternalFileDeletion(MockFileSystem fileSystem, MockFileWatcher fileWatcher, string path)
    {
        fileSystem.SimulateFileDeleted(path);
        fileWatcher.SimulateFileDeleted(path);
    }

    /// <summary>
    ///     Renames a file in the mock file system and triggers the appropriate watcher event
    /// </summary>
    public static void SimulateExternalFileRename(MockFileSystem fileSystem, MockFileWatcher fileWatcher,
        string oldPath, string newPath)
    {
        string? content = null;
        try
        {
            content = fileSystem.ReadAllTextAsync(oldPath, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (FileNotFoundException)
        {
            content = string.Empty;
        }


        fileSystem.SimulateFileCreated(newPath, content);


        fileSystem.SimulateFileDeleted(oldPath);


        fileWatcher.SimulateFileRenamed(oldPath, newPath);
    }
}
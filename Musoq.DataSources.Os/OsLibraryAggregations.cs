using System.Collections.Generic;
using System.IO;
using Musoq.DataSources.Os.Directories;
using Musoq.DataSources.Os.Files;
using Musoq.Plugins;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Os;

public partial class OsLibrary
{
    /// <summary>
    ///     Aggregates files from the current group into a list.
    /// </summary>
    /// <param name="file">File to aggregate.</param>
    /// <returns>Aggregated files.</returns>
    [AggregateFunction(typeof(AggregateFilesKernel), EmptyResultBehavior = AggregateEmptyResultBehavior.Null)]
    public IReadOnlyList<FileEntity>? AggregateFiles(FileEntity file)
    {
        return AggregateFunction.NotInvoked<IReadOnlyList<FileEntity>?>();
    }

    /// <summary>
    ///     Aggregates directories from the current group into a list.
    /// </summary>
    /// <param name="directory">Directory to aggregate.</param>
    /// <returns>Aggregated directories.</returns>
    [AggregateFunction(typeof(AggregateDirectoriesKernel), EmptyResultBehavior = AggregateEmptyResultBehavior.Null)]
    public IReadOnlyList<DirectoryInfo>? AggregateDirectories(DirectoryInfo directory)
    {
        return AggregateFunction.NotInvoked<IReadOnlyList<DirectoryInfo>?>();
    }

    /// <summary>
    ///     Aggregates directory datasource rows while retaining the original directory values.
    /// </summary>
    /// <param name="directory">Directory datasource row to aggregate.</param>
    /// <returns>Aggregated directories.</returns>
    [AggregateFunction(typeof(AggregateDirectoryEntitiesKernel), EmptyResultBehavior = AggregateEmptyResultBehavior.Null)]
    public IReadOnlyList<DirectoryInfo>? AggregateDirectories(DirectoryEntity directory)
    {
        return AggregateFunction.NotInvoked<IReadOnlyList<DirectoryInfo>?>();
    }
}

public static class AggregateFilesKernel
{
    public struct State
    {
        public List<FileEntity>? Files;
    }

    public static void Set(ref State state, FileEntity file)
    {
        state.Files ??= [];
        state.Files.Add(file);
    }

    public static IReadOnlyList<FileEntity>? Get(ref State state)
    {
        return state.Files;
    }

    public static void Merge(ref State state, ref State other)
    {
        if (other.Files == null)
            return;

        state.Files ??= [];
        state.Files.AddRange(other.Files);
    }
}

public static class AggregateDirectoriesKernel
{
    public struct State
    {
        public List<DirectoryInfo>? Directories;
    }

    public static void Set(ref State state, DirectoryInfo directory)
    {
        state.Directories ??= [];
        state.Directories.Add(directory);
    }

    public static IReadOnlyList<DirectoryInfo>? Get(ref State state)
    {
        return state.Directories;
    }

    public static void Merge(ref State state, ref State other)
    {
        if (other.Directories == null)
            return;

        state.Directories ??= [];
        state.Directories.AddRange(other.Directories);
    }
}

public static class AggregateDirectoryEntitiesKernel
{
    public struct State
    {
        public List<DirectoryInfo>? Directories;
    }

    public static void Set(ref State state, DirectoryEntity directory)
    {
        state.Directories ??= [];
        state.Directories.Add(directory.DirectoryInfo);
    }

    public static IReadOnlyList<DirectoryInfo>? Get(ref State state)
    {
        return state.Directories;
    }

    public static void Merge(ref State state, ref State other)
    {
        if (other.Directories == null)
            return;

        state.Directories ??= [];
        state.Directories.AddRange(other.Directories);
    }
}

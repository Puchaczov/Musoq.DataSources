using System;
using System.IO;

namespace Musoq.DataSources.Os.Directories;

/// <summary>
/// Public generated-execution row contract for the directories datasource.
/// </summary>
public sealed class DirectoryEntity
{
    private readonly DirectoryInfo _directoryInfo;

    /// <summary>
    /// Initializes a directory row from the operating system directory value.
    /// </summary>
    public DirectoryEntity(DirectoryInfo directoryInfo)
    {
        _directoryInfo = directoryInfo ?? throw new ArgumentNullException(nameof(directoryInfo));
    }

    /// <summary>Gets the full directory path.</summary>
    public string FullName => _directoryInfo.FullName;

    /// <summary>Gets the directory attributes.</summary>
    public FileAttributes Attributes => _directoryInfo.Attributes;

    /// <summary>Gets the creation time.</summary>
    public DateTimeOffset CreationTime => new(_directoryInfo.CreationTime);

    /// <summary>Gets the UTC creation time.</summary>
    public DateTimeOffset CreationTimeUtc => new(_directoryInfo.CreationTimeUtc);

    /// <summary>Gets the last access time.</summary>
    public DateTimeOffset LastAccessTime => new(_directoryInfo.LastAccessTime);

    /// <summary>Gets the UTC last access time.</summary>
    public DateTimeOffset LastAccessTimeUtc => new(_directoryInfo.LastAccessTimeUtc);

    /// <summary>Gets the last write time.</summary>
    public DateTimeOffset LastWriteTime => new(_directoryInfo.LastWriteTime);

    /// <summary>Gets the UTC last write time.</summary>
    public DateTimeOffset LastWriteTimeUtc => new(_directoryInfo.LastWriteTimeUtc);

    /// <summary>Gets whether the directory exists.</summary>
    public bool Exists => _directoryInfo.Exists;

    /// <summary>Gets the extension.</summary>
    public string Extension => _directoryInfo.Extension;

    /// <summary>Gets the directory name.</summary>
    public string Name => _directoryInfo.Name;

    /// <summary>Gets the parent directory.</summary>
    public DirectoryInfo? Parent => _directoryInfo.Parent;

    /// <summary>Gets the root directory path.</summary>
    public string Root => _directoryInfo.Root.FullName;

    /// <summary>Gets the underlying operating system directory value.</summary>
    public DirectoryInfo DirectoryInfo => _directoryInfo;
}

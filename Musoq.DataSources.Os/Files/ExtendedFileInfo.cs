using System;
using System.IO;

namespace Musoq.DataSources.Os.Files;

/// <summary>
/// File info for row processing
/// </summary>
public class ExtendedFileInfo
{
    /// <summary>
    /// Initializes a new instance of extended file info
    /// </summary>
    /// <param name="fileInfo">File info to be constructed from</param>
    /// <param name="computationRootDirectoryPath">Computation root directory path</param>
    public ExtendedFileInfo(FileInfo fileInfo, string computationRootDirectoryPath)
    {
        FileInfo = fileInfo;
        ComputationRootDirectoryPath = computationRootDirectoryPath;
    }

    /// <summary>
    /// Computation root directory path
    /// </summary>
    public string ComputationRootDirectoryPath { get; }

    /// <summary>
    /// Metadata about the file
    /// </summary>
    public FileInfo FileInfo { get; }

    /// <summary>
    /// Determine whether file is readonly
    /// </summary>
    public bool IsReadOnly => FileInfo.IsReadOnly;
        
    /// <summary>
    /// Determine whether file exists
    /// </summary>
    public bool Exists => FileInfo.Exists;
        
    /// <summary>
    /// Gets the directory name
    /// </summary>
    public string DirectoryName => FileInfo.DirectoryName;
        
    /// <summary>
    /// Gets the directory info
    /// </summary>
    public DirectoryInfo Directory => FileInfo.Directory;
        
    /// <summary>
    /// Gets the file length
    /// </summary>
    public long Length => FileInfo.Length;
        
    /// <summary>
    /// Gets the file name
    /// </summary>
    public string Name => FileInfo.Name;

    /// <summary>
    /// Gets the creation time
    /// </summary>
    public DateTime CreationTime => FileInfo.CreationTime;

    /// <summary>
    /// Gets the creation time UTC
    /// </summary>
    public DateTime CreationTimeUtc => FileInfo.CreationTimeUtc;

    /// <summary>
    /// Gets the extension
    /// </summary>
    public string Extension => FileInfo.Extension;

    /// <summary>
    /// Gets the full name
    /// </summary>
    public string FullName => FileInfo.FullName;

    /// <summary>
    /// Opens the file for reading
    /// </summary>
    /// <returns>FileStream</returns>
    public FileStream OpenRead() => FileInfo.OpenRead();

    /// <summary>
    /// Gets the file attributes
    /// </summary>
    public FileAttributes Attributes => FileInfo.Attributes;
}
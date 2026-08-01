using System;
using System.IO;

namespace Musoq.DataSources.Os.Runtime;

public sealed class PathInfoEntity
{
    public PathInfoEntity(string inputPath)
    {
        InputPath = inputPath;

        try
        {
            FullPath = Path.GetFullPath(inputPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            FullPath = null;
        }

        if (FullPath is null)
            return;

        IsFile = File.Exists(FullPath);
        IsDirectory = Directory.Exists(FullPath);
        Exists = IsFile || IsDirectory;
        Root = Path.GetPathRoot(FullPath);
        DirectoryName = IsDirectory ? Directory.GetParent(FullPath)?.FullName : Path.GetDirectoryName(FullPath);
        FileName = Path.GetFileName(FullPath);
        Extension = Path.GetExtension(FullPath);
    }

    public string InputPath { get; }
    public string? FullPath { get; }
    public bool Exists { get; }
    public bool IsFile { get; }
    public bool IsDirectory { get; }
    public string? Root { get; }
    public string? DirectoryName { get; }
    public string? FileName { get; }
    public string? Extension { get; }
}

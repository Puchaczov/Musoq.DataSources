using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MetadataExtractor;
using Musoq.DataSources.Os.Files;
using Musoq.DataSources.Os.Zip;
using Musoq.Plugins;
using Musoq.Plugins.Attributes;
using Musoq.Plugins.Helpers;
using Musoq.Schema.Exceptions;
using Directory = System.IO.Directory;

namespace Musoq.DataSources.Os;

/// <summary>
/// Operating system schema helper methods
/// </summary>
[BindableClass]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public partial class OsLibrary : LibraryBase
{
    private static readonly HashSet<string> IsZipArchiveSet =
    [
        ".zip",
        ".jar",
        ".war",
        ".ear"
    ];

    private static readonly HashSet<string> IsArchiveSet =
    [
        ".7z",
        ".bz2",
        ".bzip2",
        ".gzip",
        ".lz",
        ".rar",
        ".tar",
        ".xz",
        ".zip"
    ];

    private static readonly HashSet<string> IsAudioSet =
    [
        ".aac",
        ".aiff",
        ".amr",
        ".flac",
        ".gsm",
        ".m4a",
        ".m4b",
        ".m4p",
        ".mp3",
        ".ogg",
        ".wma",
        ".aa",
        ".aax",
        ".ape",
        ".dsf",
        ".mpc",
        ".mpp",
        ".oga",
        ".wav",
        ".wv",
        ".webm"
    ];

    private static readonly HashSet<string> IsBookSet =
    [
        ".azw3",
        ".chm",
        ".djvu",
        ".epub",
        ".fb2",
        ".mobi",
        ".pdf"
    ];

    private static readonly HashSet<string> IsDocSet =
    [
        ".accdb",
        ".doc",
        ".docm",
        ".docx",
        ".dot",
        ".dotm",
        ".dotx",
        ".mdb",
        ".ods",
        ".odt",
        ".pdf",
        ".potm",
        ".potx",
        ".ppt",
        ".pptm",
        ".pptx",
        ".rtf",
        ".xlm",
        ".xls",
        ".xlsm",
        ".xlsx",
        ".xlt",
        ".xltm",
        ".xltx",
        ".xps"
    ];

    private static readonly HashSet<string> IsImageSet =
    [
        ".bmp",
        ".gif",
        ".jpeg",
        ".jpg",
        ".png",
        ".psb",
        ".psd",
        ".tiff",
        ".webp",
        ".pbm",
        ".pgm",
        ".ppm",
        ".pnm",
        ".pcx",
        ".dng",
        ".svg"
    ];

    private static readonly HashSet<string> IsSourceSet =
    [
        ".asm",
        ".bas",
        ".c",
        ".cc",
        ".ceylon",
        ".clj",
        ".coffee",
        ".cpp",
        ".cs",
        ".dart",
        ".elm",
        ".erl",
        ".go",
        ".groovy",
        ".h",
        ".hh",
        ".hpp",
        ".java",
        ".js",
        ".jsp",
        ".kt",
        ".kts",
        ".lua",
        ".nim",
        ".pas",
        ".php",
        ".pl",
        ".pm",
        ".py",
        ".rb",
        ".rs",
        ".scala",
        ".swift",
        ".tcl",
        ".vala",
        ".vb"
    ];

    private static readonly HashSet<string> IsVideoSet =
    [
        ".3gp",
        ".avi",
        ".flv",
        ".m4p",
        ".m4v",
        ".mkv",
        ".mov",
        ".mp4",
        ".mpeg",
        ".mpg",
        ".webm",
        ".wmv",
        ".ogv",
        ".asf",
        ".mpe",
        ".mpv",
        ".m2v"
    ];

    /// <summary>
    /// Determines whether the extension is zip archive.
    /// </summary>
    /// <param name="extension">Extension that needs to be examined</param>
    /// <returns><see langword="true" />if the specified extension is zip archive; otherwise, <see langword="false" /></returns>
    [BindableMethod]
    public bool IsZipArchive(string extension) => IsZipArchiveSet.Contains(extension);

    /// <summary>
    /// Determines whether the extension is zip archive.
    /// </summary>
    /// <param name="fileInfo">FileInfo that must be examined whether is zip or not</param>
    /// <returns><see langword="true" />if the specified extension is zip archive; otherwise, <see langword="false" /></returns>
    [BindableMethod]
    public bool IsZipArchive([InjectSpecificSource(typeof(FileEntity))] FileEntity fileInfo) => IsZipArchiveSet.Contains(fileInfo.Extension);

    /// <summary>
    /// Determine whether the extension is archive.
    /// </summary>
    /// <param name="extension">The extension</param>
    /// <returns>True if archive; otherwise false</returns>
    [BindableMethod]
    public bool IsArchive(string extension) => IsArchiveSet.Contains(extension);

    /// <summary>
    /// Determines whether the file is archive.
    /// </summary>
    /// <param name="fileInfo">FileInfo that must be examined whether is archive or not</param>
    /// <returns><see langword="true" />if the specified extension is archive; otherwise, <see langword="false" /></returns>
    [BindableMethod]
    public bool IsArchive([InjectSpecificSource(typeof(FileEntity))] FileEntity fileInfo) => IsArchiveSet.Contains(fileInfo.Extension);

    /// <summary>
    /// Determine whether the extension is audio.
    /// </summary>
    /// <param name="extension">The extension</param>
    /// <returns>True if specified extension is audio; otherwise false</returns>
    [BindableMethod]
    public bool IsAudio(string extension) => IsAudioSet.Contains(extension);
        
    /// <summary>
    /// Determine whether the extension is audio.
    /// </summary>
    /// <param name="fileInfo">The fileInfo</param>
    /// <returns>True if audio; otherwise false</returns>
    [BindableMethod]
    public bool IsAudio([InjectSpecificSource(typeof(FileEntity))] FileEntity fileInfo) => IsAudioSet.Contains(fileInfo.Extension);

    /// <summary>
    /// Determine whether the extension is book.
    /// </summary>
    /// <param name="extension">The extension</param>
    /// <returns>True if specified extension is book; otherwise false</returns>
    [BindableMethod]
    public bool IsBook(string extension) => IsBookSet.Contains(extension);

    /// <summary>
    /// Determine whether the extension is book.
    /// </summary>
    /// <param name="fileInfo">The fileInfo</param>
    /// <returns>True if book; otherwise false</returns>
    [BindableMethod]
    public bool IsBook([InjectSpecificSource(typeof(FileEntity))] FileEntity fileInfo) => IsBookSet.Contains(fileInfo.Extension);

    /// <summary>
    /// Determine whether the extension is document.
    /// </summary>
    /// <param name="extension">The extension</param>
    /// <returns>True if specified extension is document; otherwise false</returns>
    [BindableMethod]
    public bool IsDoc(string extension) => IsDocSet.Contains(extension);

    /// <summary>
    /// Determine whether the extension is document.
    /// </summary>
    /// <param name="fileInfo">The fileInfo</param>
    /// <returns>True if document; otherwise false</returns>
    [BindableMethod]
    public bool IsDoc([InjectSpecificSource(typeof(FileEntity))] FileEntity fileInfo) => IsDocSet.Contains(fileInfo.Extension);
        
    /// <summary>
    /// Determine whether the extension is image.
    /// </summary>
    /// <param name="extension">The extension</param>
    /// <returns>True if specified extension is image; otherwise false</returns>
    [BindableMethod]
    public bool IsImage(string extension) => IsImageSet.Contains(extension);
        
    /// <summary>
    /// Determine whether the extension is image.
    /// </summary>
    /// <param name="fileInfo">The fileInfo</param>
    /// <returns>True if image; otherwise false</returns>
    [BindableMethod]
    public bool IsImage([InjectSpecificSource(typeof(FileEntity))] FileEntity fileInfo) => IsImageSet.Contains(fileInfo.Extension);

    /// <summary>
    /// Determine whether the extension is source.
    /// </summary>
    /// <param name="extension">The extension</param>
    /// <returns>True if specified extension is source; otherwise false</returns>
    [BindableMethod]
    public bool IsSource(string extension) => IsSourceSet.Contains(extension);
        
    /// <summary>
    /// Determine whether the extension is source.
    /// </summary>
    /// <param name="fileInfo">The fileInfo</param>
    /// <returns>True if source; otherwise false</returns>
    [BindableMethod]
    public bool IsSource([InjectSpecificSource(typeof(FileEntity))] FileEntity fileInfo) => IsSourceSet.Contains(fileInfo.Extension);

    /// <summary>
    /// Determine whether the extension is video.
    /// </summary>
    /// <param name="extension">The extension</param>
    /// <returns>True if specified extension is video; otherwise false</returns>
    [BindableMethod]
    public bool IsVideo(string extension) => IsVideoSet.Contains(extension);

    /// <summary>
    /// Determine whether the extension is video.
    /// </summary>
    /// <param name="fileInfo">The fileInfo</param>
    /// <returns>True if video; otherwise false</returns>
    [BindableMethod]
    public bool IsVideo([InjectSpecificSource(typeof(FileEntity))] FileEntity fileInfo) => IsVideoSet.Contains(fileInfo.Extension);

    /// <summary>
    /// Gets the file content
    /// </summary>
    /// <param name="extendedFileInfo">The extendedFileInfo</param>
    /// <returns>String content of a file</returns>
    [BindableMethod]
    public string? GetFileContent([InjectSpecificSource(typeof(FileEntity))] FileEntity extendedFileInfo)
    {
        if (!extendedFileInfo.Exists)
            return null;

        using var file = extendedFileInfo.OpenRead();
        using var fileReader = new StreamReader(file);
        return fileReader.ReadToEnd();
    }

    /// <summary>
    /// Gets the relative path of a file
    /// </summary>
    /// <param name="fileInfo">The fileInfo</param>
    /// <returns>Relative file path to ComputationRootDirectoryPath</returns>
    [BindableMethod]
    public string GetRelativePath([InjectSpecificSource(typeof(FileEntity))] FileEntity fileInfo)
    {
        return fileInfo.FullPath.Replace(fileInfo.ComputationRootDirectoryPath, string.Empty);
    }

    /// <summary>
    /// Gets the relative path of a file
    /// </summary>
    /// <param name="fileInfo">The fileInfo</param>
    /// <param name="basePath">The basePath</param>
    /// <returns>Relative file path to basePath</returns>
    [BindableMethod]
    public string GetRelativePath([InjectSpecificSource(typeof(FileEntity))] FileEntity fileInfo, string basePath)
    {
        if (basePath == null)
            throw new ArgumentNullException(nameof(basePath));

        if (!Directory.Exists(basePath))
            throw new DirectoryNotFoundException(basePath);

        basePath = new DirectoryInfo(basePath).FullName;

        return fileInfo.FullPath.Replace(basePath, string.Empty);
    }

    /// <summary>
    /// Gets head bytes of a file
    /// </summary>
    /// <param name="file">The file</param>
    /// <param name="length">The length</param>
    /// <returns>Head bytes of a file</returns>
    [BindableMethod]
    public byte[] Head([InjectSpecificSource(typeof(FileEntity))] FileEntity file, int length)
        => GetFileBytes(file, length, 0);

    /// <summary>
    /// Gets tail bytes of a file
    /// </summary>
    /// <param name="file">The file</param>
    /// <param name="length">The length</param>
    /// <returns>Tail bytes of a file</returns>
    [BindableMethod]
    public byte[] Tail([InjectSpecificSource(typeof(FileEntity))] FileEntity file, int length)
    {
        if (file == null)
            throw new InjectSourceNullReferenceException(typeof(FileInfo));

        using var stream = file.OpenRead();
        using var reader = new BinaryReader(stream);
        var toRead = length < stream.Length ? length : stream.Length;

        var bytes = new byte[toRead];

        stream.Position = stream.Length - length;
        for (var i = 0; i < toRead; ++i)
            bytes[i] = reader.ReadByte();

        return bytes;
    }

    /// <summary>
    /// Gets file bytes of a file
    /// </summary>
    /// <param name="file">The file</param>
    /// <param name="bytesCount">The bytesCount</param>
    /// <param name="offset">The offset</param>
    /// <returns>Bytes of a file</returns>
    [BindableMethod]
    public byte[] GetFileBytes([InjectSpecificSource(typeof(FileEntity))] FileEntity file, long bytesCount = long.MaxValue, long offset = 0)
    {
        if (file == null)
            throw new InjectSourceNullReferenceException(typeof(FileInfo));

        using var stream = file.OpenRead();
        using var reader = new BinaryReader(stream);
        if (offset > 0)
            stream.Seek(offset, SeekOrigin.Begin);

        var toRead = bytesCount < stream.Length ? bytesCount : stream.Length;

        var bytes = new byte[toRead];

        for (var i = 0; i < toRead; ++i)
            bytes[i] = reader.ReadByte();

        return bytes;
    }

    /// <summary>
    /// Computes Sha1 hash of a file
    /// </summary>
    /// <param name="file">The file</param>
    /// <returns>Sha1 of a file</returns>
    [BindableMethod]
    public string Sha1File([InjectSpecificSource(typeof(FileEntity))] FileEntity file)
    {
        if (file == null)
            throw new InjectSourceNullReferenceException(typeof(FileEntity));

        using var stream = file.OpenRead();
        return HashHelper.ComputeHash(stream, SHA1.Create);
    }

    /// <summary>
    /// Computes Sha256 hash of a file
    /// </summary>
    /// <param name="file">The file</param>
    /// <returns>Sha256 of a file</returns>
    [BindableMethod]
    public string Sha256File([InjectSpecificSource(typeof(FileEntity))] FileEntity file)
    {
        if (file == null)
            throw new InjectSourceNullReferenceException(typeof(FileEntity));

        return Sha256File(file.FileInfo);
    }
        
    /// <summary>
    /// Computes Sha256 hash of a file
    /// </summary>
    /// <param name="file">The file</param>
    /// <returns>Sha1 of a file</returns>
    public string Sha256File(FileInfo file)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));

        using var stream = file.OpenRead();
        return HashHelper.ComputeHash(stream, SHA256.Create);
    }

    /// <summary>
    /// Computes Md5 hash of a file
    /// </summary>
    /// <param name="file">The file</param>
    /// <returns>Md5 of a file</returns>
    [BindableMethod]
    public string Md5File([InjectSpecificSource(typeof(FileEntity))] FileEntity file)
    {
        if (file == null)
            throw new InjectSourceNullReferenceException(typeof(FileEntity));

        using var stream = file.OpenRead();
        return HashHelper.ComputeHash(stream, MD5.Create);
    }
    
    /// <summary>
    /// Turns file into base64 string
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    /// <exception cref="InjectSourceNullReferenceException"></exception>
    [BindableMethod]
    public string Base64File([InjectSpecificSource(typeof(FileEntity))] FileEntity file)
    {
        if (file == null)
            throw new InjectSourceNullReferenceException(typeof(FileEntity));

        using var stream = file.OpenRead();
        using var reader = new BinaryReader(stream);
        var bytes = reader.ReadBytes((int)stream.Length);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Determine whether file has specific content
    /// </summary>
    /// <param name="file">The file</param>
    /// <param name="pattern">The pattern</param>
    /// <returns>True if has content; otherwise false</returns>
    /// <exception cref="InjectSourceNullReferenceException"></exception>
    [BindableMethod]
    public bool HasContent([InjectSpecificSource(typeof(FileEntity))] FileEntity file, string pattern)
    {
        if (file == null)
            throw new InjectSourceNullReferenceException(typeof(FileEntity));

        using var stream = new StreamReader(file.OpenRead());
        var content = stream.ReadToEnd();
        return Regex.IsMatch(content, pattern);
    }

    /// <summary>
    /// Determine whether file has attribute
    /// </summary>
    /// <param name="file">The file</param>
    /// <param name="flags">The flags</param>
    /// <returns>True if has attribute; otherwise false</returns>
    [BindableMethod]
    public bool HasAttribute([InjectSpecificSource(typeof(FileEntity))] FileEntity file, long flags)
    {
        return (flags & Convert.ToUInt32(file.Attributes)) == flags;
    }

    /// <summary>
    /// Gets lines containing word
    /// </summary>
    /// <param name="file">The file</param>
    /// <param name="word">The word</param>
    /// <returns>Line containing searched word</returns>
    [BindableMethod]
    public string GetLinesContainingWord([InjectSpecificSource(typeof(FileEntity))] FileEntity file, string word)
    {
        if (file == null)
            throw new InjectSourceNullReferenceException(typeof(FileEntity));

        using var stream = new StreamReader(file.OpenRead());
        var lines = new List<string>();
        var line = 1;
        while (!stream.EndOfStream)
        {
            var strLine = stream.ReadLine();
            if (strLine != null && strLine.Contains(word))
                lines.Add(line.ToString());
            line += 1;
        }

        var builder = new StringBuilder("(");

        for (int i = 0, j = lines.Count - 1; i < j; ++i) builder.Append(lines[i]);

        builder.Append(lines[lines.Count]);
        builder.Append(')');

        return builder.ToString();
    }

    /// <summary>
    /// Gets the file length
    /// </summary>
    /// <param name="context">The context</param>
    /// <param name="unit">The unit</param>
    /// <returns>File length</returns>
    [BindableMethod]
    public long GetFileLength([InjectSpecificSource(typeof(FileEntity))] FileEntity context, string unit = "b")
        => GetLengthOfFile(context, unit);

    /// <summary>
    /// Gets the file length
    /// </summary>
    /// <param name="context">The context</param>
    /// <param name="unit">The unit</param>
    /// <returns>File length</returns>
    [BindableMethod]
    public long GetLengthOfFile([InjectSpecificSource(typeof(FileEntity))] FileEntity context, string unit = "b")
    {
        if (context == null)
            throw new InjectSourceNullReferenceException(typeof(FileEntity));

        return unit.ToLowerInvariant() switch
        {
            "b" => context.Length,
            "kb" => Convert.ToInt64(context.Length / 1024f),
            "mb" => Convert.ToInt64(context.Length / 1024f / 1024f),
            "gb" => Convert.ToInt64(context.Length / 1024f / 1024f / 1024f),
            _ => throw new NotSupportedException($"unsupported unit ({unit})")
        };
    }

    /// <summary>
    /// Gets the SubPath from the path
    /// </summary>
    /// <param name="context">The context</param>
    /// <param name="nesting">The nesting</param>
    /// <returns>SubPath based on nesting</returns>
    [BindableMethod]
    public string? SubPath([InjectSpecificSource(typeof(DirectoryInfo))] DirectoryInfo context, int nesting)
        => SubPath(context.FullName, nesting);

    /// <summary>
    /// Gets the SubPath from the path
    /// </summary>
    /// <param name="context">The context</param>
    /// <param name="nesting">The nesting</param>
    /// <returns>SubPath based on nesting</returns>
    [BindableMethod]
    public string? SubPath([InjectSpecificSource(typeof(FileEntity))] FileEntity context, int nesting)
        => SubPath(context.Directory.FullName, nesting);

    /// <summary>
    /// Gets the relative SubPath from the path
    /// </summary>
    /// <param name="context">The context</param>
    /// <param name="nesting">The nesting</param>
    /// <returns>Relative subPath based on nesting</returns>
    [BindableMethod]
    public string? RelativeSubPath([InjectSpecificSource(typeof(FileEntity))] FileEntity context, int nesting)
        => SubPath(GetRelativePath(context, context.ComputationRootDirectoryPath), nesting);

    /// <summary>
    /// Gets the relative SubPath from the path
    /// </summary>
    /// <param name="directoryPath">The directoryPath</param>
    /// <param name="nesting">The nesting</param>
    /// <returns>Relative subPath based on nesting</returns>
    [BindableMethod]
    public string? SubPath(string? directoryPath, int nesting)
    {
        if (directoryPath == null)
            return null;

        if (directoryPath == string.Empty)
            return null;

        if (nesting < 0)
            return string.Empty;

        var splitDirs = directoryPath.Split(Path.DirectorySeparatorChar);
        var subPathBuilder = new StringBuilder();

        subPathBuilder.Append(splitDirs[0]);

        if (nesting >= 1 && splitDirs.Length > 1)
        {
            subPathBuilder.Append(Path.DirectorySeparatorChar);

            for (int i = 1; i < nesting && i < splitDirs.Length - 1; ++i)
            {
                subPathBuilder.Append(splitDirs[i]);
                subPathBuilder.Append(Path.DirectorySeparatorChar);
            }

            subPathBuilder.Append(splitDirs[nesting < splitDirs.Length - 1 ? nesting : splitDirs.Length - 1]);
        }

        return subPathBuilder.ToString();
    }

    /// <summary>
    /// Gets the length of the file
    /// </summary>
    /// <param name="context">The context</param>
    /// <param name="unit">The unit</param>
    /// <returns>Length of a file</returns>
    [BindableMethod]
    public long Length([InjectSpecificSource(typeof(FileEntity))] FileEntity context, string unit = "b")
        => GetLengthOfFile(context, unit);

    /// <summary>
    /// Gets the file info
    /// </summary>
    /// <param name="fullPath">The fullPath</param>
    /// <returns>ExtendedFileInfo</returns>
    [BindableMethod]
    public FileEntity? GetFileInfo(string fullPath)
    {
        var fileInfo = new FileInfo(fullPath);
        
        if (!fileInfo.Exists)
            return null;
        
        if (fileInfo.DirectoryName == null)
            throw new InvalidOperationException("Directory name is null.");
        
        return new FileEntity(fileInfo, fileInfo.DirectoryName);
    }

    /// <summary>
    /// Gets extended file info
    /// </summary>
    /// <param name="context">The context</param>
    /// <returns>ExtendedFileInfo</returns>
    [BindableMethod]
    public FileEntity GetExtendedFileInfo([InjectSpecificSource(typeof(FileEntity))] FileEntity context)
        => context;

    /// <summary>
    /// Gets the count of lines of a file
    /// </summary>
    /// <param name="context">The context</param>
    /// <returns>ExtendedFileInfo</returns>
    [BindableMethod]
    public long CountOfLines([InjectSpecificSource(typeof(FileEntity))] FileEntity context)
    {
        if (context == null)
            throw new InjectSourceNullReferenceException(typeof(FileEntity));

        using var stream = new StreamReader(context.OpenRead());
        var lines = 0;
        while (!stream.EndOfStream)
        {
            lines += 1;
            stream.ReadLine();
        }

        return lines;
    }

    /// <summary>
    /// Gets the count of non empty lines of a file
    /// </summary>
    /// <param name="context">The context</param>
    /// <returns>Count of non empty lines</returns>
    [BindableMethod]
    public long CountOfNotEmptyLines([InjectSpecificSource(typeof(FileEntity))] FileEntity context)
    {
        if (context == null)
            throw new InjectSourceNullReferenceException(typeof(FileEntity));

        using var stream = new StreamReader(context.OpenRead());
        var lines = 0;
        while (!stream.EndOfStream)
        {
            var line = stream.ReadLine();

            if (line == string.Empty)
                continue;

            lines += 1;
        }

        return lines;
    }

    /// <summary>
    /// Combines the paths
    /// </summary>
    /// <param name="path1">The path1</param>
    /// <param name="path2">The path2</param>
    /// <returns>Combined paths</returns>
    [BindableMethod]
    public string Combine(string path1, string path2)
    {
        return Path.Combine(path1, path2);
    }
        
    /// <summary>
    /// Combines the paths
    /// </summary>
    /// <param name="path1">The path1</param>
    /// <param name="path2">The path2</param>
    /// <param name="path3">The path3</param>
    /// <returns>Combined paths</returns>
    [BindableMethod]
    public string Combine(string path1, string path2, string path3)
    {
        return Path.Combine(path1, path2, path3);
    }
        
    /// <summary>
    /// Combines the paths
    /// </summary>
    /// <param name="path1">The path1</param>
    /// <param name="path2">The path2</param>
    /// <param name="path3">The path3</param>
    /// <param name="path4">The path4</param>
    /// <returns>Combined paths</returns>
    [BindableMethod]
    public string Combine(string path1, string path2, string path3, string path4)
    {
        return Path.Combine(path1, path2, path3, path4);
    }
        
    /// <summary>
    /// Combines the paths
    /// </summary>
    /// <param name="path1">The path1</param>
    /// <param name="path2">The path2</param>
    /// <param name="path3">The path3</param>
    /// <param name="path4">The path4</param>
    /// <param name="path5">The path5</param>
    /// <returns>Combined paths</returns>
    [BindableMethod]
    public string Combine(string path1, string path2, string path3, string path4, string path5)
    {
        return Path.Combine(path1, path2, path3, path4, path5);
    }
        
    /// <summary>
    /// Combines the paths
    /// </summary>
    /// <param name="paths">The paths</param>
    /// <returns>Combined paths</returns>
    [BindableMethod]
    public string Combine(params string[] paths)
    {
        return Path.Combine(paths);
    }
    
    /// <summary>
    /// Gets the directory name
    /// </summary>
    /// <param name="path">The path</param>
    /// <returns>Directory name</returns>
    [BindableMethod]
    public string? GetDirectoryName(string? path)
    {
        return Path.GetDirectoryName(path);
    }
    
    /// <summary>
    /// Gets the file name
    /// </summary>
    /// <param name="path">The path</param>
    /// <returns>File name</returns>
    [BindableMethod]
    public string? GetFileName(string? path)
    {
        return Path.GetFileName(path);
    }
    
    /// <summary>
    /// Gets the file name without extension
    /// </summary>
    /// <param name="path">The path</param>
    /// <returns>File name without extension</returns>
    [BindableMethod]
    public string? GetFileNameWithoutExtension(string? path)
    {
        return Path.GetFileNameWithoutExtension(path);
    }
    
    /// <summary>
    /// Gets the extension
    /// </summary>
    /// <param name="path">The path</param>
    /// <returns>Extension</returns>
    [BindableMethod]
    public string? GetExtension(string? path)
    {
        return Path.GetExtension(path);
    }
    
    /// <summary>
    /// Gets the metadata of a file
    /// </summary>
    /// <param name="fileInfo">The fileInfo</param>
    /// <param name="directoryName">The directoryName</param>
    /// <param name="tagName">The tagName</param>
    /// <returns>Metadata of a file</returns>
    [BindableMethod]
    public string? GetMetadata([InjectSpecificSource(typeof(FileEntity))] FileEntity fileInfo, string directoryName, string tagName)
    {
        foreach (var directory in fileInfo.Metadata)
        {
            if (directory.Name != directoryName) continue;
            
            foreach (var tag in directory.Tags)
            {
                if (tag.Name == tagName)
                    return tag.Description;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets the metadata of a file
    /// </summary>
    /// <param name="fileInfo">The fileInfo</param>
    /// <param name="tagName">The tagName</param>
    /// <returns>Metadata of a file</returns>
    [BindableMethod]
    public string? GetMetadata([InjectSpecificSource(typeof(FileEntity))] FileEntity fileInfo, string tagName)
    {
        foreach (var directory in fileInfo.Metadata)
        {
            foreach (var tag in directory.Tags)
            {
                if (tag.Name == tagName)
                    return tag.Description;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Checks whether file has metadata directory
    /// </summary>
    /// <param name="fileInfo">The fileInfo</param>
    /// <param name="directoryName">The directoryName</param>
    /// <returns>True if has metadata directory; otherwise false</returns>
    [BindableMethod]
    public bool HasMetadataDirectory([InjectSpecificSource(typeof(FileEntity))] FileEntity fileInfo, string directoryName)
    {
        return fileInfo.Metadata.Any(directory => directory.Name == directoryName);
    }
    
    /// <summary>
    /// Checks whether file has metadata tag
    /// </summary>
    /// <param name="fileInfo">The fileInfo</param>
    /// <param name="directoryName">The directoryName</param>
    /// <param name="tagName">The tagName</param>
    /// <returns>True if has metadata tag; otherwise false</returns>
    [BindableMethod]
    public bool HasMetadataTag([InjectSpecificSource(typeof(FileEntity))] FileEntity fileInfo, string directoryName, string tagName)
    {
        foreach (var directory in fileInfo.Metadata)
        {
            if (directory.Name != directoryName) continue;
            
            foreach (var tag in directory.Tags)
            {
                if (tag.Name == tagName)
                    return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Checks whether file has metadata tag
    /// </summary>
    /// <param name="fileInfo">The fileInfo</param>
    /// <param name="tagName">The tagName</param>
    /// <returns>True if has metadata tag; otherwise false</returns>
    [BindableMethod]
    public bool HasMetadataTag([InjectSpecificSource(typeof(FileEntity))] FileEntity fileInfo, string tagName)
    {
        return fileInfo.Metadata.Any(directory => directory.Tags.Any(tag => tag.Name == tagName));
    }
    
    /// <summary>
    /// Gets all metadata of a file and returns it as json
    /// </summary>
    /// <param name="fileInfo">The fileInfo</param>
    /// <returns>All metadata of a file in a json format</returns>
    [BindableMethod]
    public string AllMetadataJson([InjectSpecificSource(typeof(FileEntity))] FileEntity fileInfo)
    {
        return JsonSerializer.Serialize(fileInfo.Metadata.GroupBy(f => f.Name).Select(f => new
        {
            Directory = f.Key,
            Tags = f.SelectMany(t => t.Tags.Select(tag => new
            {
                Tag = tag.Name, tag.Description
            }))
        }));
    }
}
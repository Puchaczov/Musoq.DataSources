using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MetadataExtractor;
using MetadataExtractor.Util;
using Musoq.DataSources.Os.Exceptions;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Metadata;

internal class MetadataSource : EnumerateFilesSourceBase<MetadataEntity>
{
    public enum PathType
    {
        MustBeDirectory,
        DirectoryOrFile
    }

    private readonly string? _fileName;
    private readonly bool _throwOnMetadataReadError;

    public MetadataSource(
        string directoryPath,
        string? fileName,
        bool useSubDirectories,
        PathType pathType,
        bool throwOnMetadataReadError,
        SourceExecutionContext executionContext)
        : base(directoryPath, useSubDirectories, executionContext)
    {
        if (pathType == PathType.MustBeDirectory && fileName is not null)
            throw new NotSupportedException("File name must be null when path type is directory.");

        _fileName = fileName;
        _throwOnMetadataReadError = throwOnMetadataReadError;
    }

    protected override FileInfo[] GetFiles(DirectoryInfo directoryInfo)
    {
        if (_fileName is not null)
            return [new FileInfo(Path.Combine(directoryInfo.FullName, _fileName))];

        return base.GetFiles(directoryInfo);
    }

    protected override void ProcessFile(FileInfo file, DirectorySourceSearchOptions source, List<MetadataEntity> dirFiles)
    {
        using var stream = file.OpenRead();

        if (FileTypeDetector.DetectFileType(stream) == FileType.Unknown)
            return;

        try
        {
            dirFiles.AddRange(
                ImageMetadataReader.ReadMetadata(stream)
                    .SelectMany(directory => directory.Tags,
                        (directory, tag) =>
                            new MetadataEntity(file.FullName, directory.Name, tag.Name, tag.Description)));
        }
        catch (Exception exc)
        {
            if (_throwOnMetadataReadError)
                throw new MetadataReadException(file, exc);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using MetadataExtractor;
using MetadataExtractor.Util;
using Musoq.DataSources.Os.Exceptions;
using Musoq.Schema.Optimization;
using Musoq.DataSources.Common;

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

    protected override PatternColumnBindings<string> NativeBindings => OsNativePatterns.Metadata;

    protected override IEnumerable<FileInfo> GetFiles(DirectoryInfo directoryInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _fileName is not null ? GetFiles(directoryInfo) : base.GetFiles(directoryInfo, cancellationToken);
    }

    protected override IEnumerable<FileInfo> GetFiles(DirectoryInfo directoryInfo)
    {
        if (_fileName is not null)
        {
            var fullPath = Path.Combine(directoryInfo.FullName, _fileName);
            return AdmitPath(fullPath) ? [new FileInfo(fullPath)] : [];
        }

        return base.GetFiles(directoryInfo);
    }

    protected override void ProcessFile(
        FileInfo file, DirectorySourceSearchOptions source, Action<MetadataEntity> addRow, CancellationToken cancellationToken)
    {
        NativePatterns?.ContentOpened();
        IReadOnlyList<MetadataExtractor.Directory> directories;
        using (var stream = file.OpenRead())
        {
            if (FileTypeDetector.DetectFileType(stream) == FileType.Unknown)
                return;

            try
            {
                directories = ImageMetadataReader.ReadMetadata(stream);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                HandleMetadataReadError(file, error, cancellationToken);
                return;
            }
        }

        using var rows = directories.SelectMany(directory => directory.Tags,
            (directory, tag) => new MetadataEntity(file.FullName, directory.Name, tag.Name, tag.Description))
            .GetEnumerator();
        var constructed = 0;
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!rows.MoveNext())
                        break;
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    HandleMetadataReadError(file, error, cancellationToken);
                    return;
                }

                constructed++;
                addRow(rows.Current);
            }
        }
        finally
        {
            NativePatterns?.Constructed(constructed);
        }
    }

    private void HandleMetadataReadError(FileInfo file, Exception error, CancellationToken cancellationToken)
    {
        if (_throwOnMetadataReadError)
        {
            if (OsSchema.Reports.TryMap(error, cancellationToken, out var failure))
                throw failure;
            throw new MetadataReadException(file, error);
        }

        if (!OsSchema.Reports.Warn(ExecutionContext, error, "The existing metadata-skip option was used.", cancellationToken))
            ExceptionDispatchInfo.Throw(error);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Musoq.DataSources.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Files;

internal sealed class FileSource(string path, SourceExecutionContext executionContext) : RowSourceBase<FileEntity>
{
    private const string FileSourceName = "file";

    protected override void CollectChunks(IChunkWriter<FileEntity> writer)
    {
        var progress = new DataSourceProgressReporter(executionContext, FileSourceName);
        progress.Begin();
        long totalRowsProcessed = 0;

        try
        {
            writer.CancellationToken.ThrowIfCancellationRequested();

            var file = TryCreateFileInfo(path);
            if (file is null || !file.Exists)
            {
                progress.RowsKnown(0);
                return;
            }

            progress.RowsKnown(1);
            progress.RowRead();

            var rootDirectory = file.DirectoryName ?? Path.GetPathRoot(file.FullName) ?? string.Empty;
            var entity = new FileEntity(file, rootDirectory);

            if (!OsSourcePlanner.Matches(executionContext.Plan.AcceptedPredicate, entity))
                return;

            writer.Write(new List<FileEntity> { entity });
            totalRowsProcessed = 1;
        }
        finally
        {
            progress.End(totalRowsProcessed);
        }
    }

    private static FileInfo? TryCreateFileInfo(string path)
    {
        try
        {
            return new FileInfo(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

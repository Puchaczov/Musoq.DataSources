using System.Collections.Generic;
using System.IO;
using System.Linq;
using Musoq.DataSources.Common;
using Musoq.DataSources.Os.Files;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Compare.Directories;

internal class CompareDirectoriesSource(
    string firstDirectory,
    string secondDirectory,
    SourceExecutionContext executionContext)
    : RowSourceBase<CompareDirectoriesResult>
{
    private const string CompareDirectoriesSourceName = "compare_directories";
    private readonly DirectoryInfo _firstDirectory = new(firstDirectory);
    private readonly DirectoryInfo _secondDirectory = new(secondDirectory);

    protected override void CollectChunks(IChunkWriter<CompareDirectoriesResult> writer)
    {
        var progress = new DataSourceProgressReporter(executionContext, CompareDirectoriesSourceName);
        progress.Begin();
        long totalRowsProcessed = 0;

        try
        {
            var leftJoinedFiles = from firstDirFile in GetAllFiles(_firstDirectory)
                join secondDirFile in GetAllFiles(_secondDirectory) on
                    firstDirFile.FullPath.Replace(_firstDirectory.FullName, string.Empty) equals secondDirFile.FullPath
                        .Replace(_secondDirectory.FullName, string.Empty) into files
                from secondDirFile in files.DefaultIfEmpty()
                select new SourceDestinationFilesPair([firstDirFile, secondDirFile]);

            var rightJoinedFiles = from secondDirFile in GetAllFiles(_secondDirectory)
                where !File.Exists(Path.Combine(_firstDirectory.FullName,
                    secondDirFile.FullPath.Replace(_secondDirectory.FullName, string.Empty).Trim('\\')))
                select new SourceDestinationFilesPair([null, secondDirFile]);

            var allFiles = leftJoinedFiles.Concat(rightJoinedFiles);
            var lib = new OsLibrary();
            var chunk = new List<CompareDirectoriesResult>(100);

            foreach (var files in allFiles)
            {
                writer.CancellationToken.ThrowIfCancellationRequested();
                progress.RowRead();

                State result;

                if (files.Source != null && files.Destination != null)
                    result = lib.Sha256File(files.Source) != lib.Sha256File(files.Destination)
                        ? State.Modified
                        : State.TheSame;
                else if (files.Source != null)
                    result = State.Removed;
                else if (files.Destination != null)
                    result = State.Added;
                else
                    continue;

                chunk.Add(new CompareDirectoriesResult(_firstDirectory, files.Source, _secondDirectory,
                    files.Destination, result));
                totalRowsProcessed++;

                if (chunk.Count <= 100)
                    continue;

                writer.Write(chunk);
                chunk = [];
            }

            if (chunk.Count > 0)
                writer.Write(chunk);
        }
        finally
        {
            progress.End(totalRowsProcessed);
        }
    }

    private static IEnumerable<FileEntity> GetAllFiles(DirectoryInfo directory)
    {
        var dirs = new Stack<DirectoryInfo>();
        dirs.Push(directory);

        while (dirs.Count > 0)
        {
            var currentDir = dirs.Pop();

            foreach (var file in currentDir.GetFiles())
                yield return new FileEntity(file, directory.FullName);

            foreach (var dir in currentDir.GetDirectories())
                dirs.Push(dir);
        }
    }
}

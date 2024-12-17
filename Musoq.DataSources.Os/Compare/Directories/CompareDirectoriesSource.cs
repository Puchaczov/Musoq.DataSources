using Musoq.Schema.DataSources;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Musoq.DataSources.Os.Files;
using Musoq.Schema;

namespace Musoq.DataSources.Os.Compare.Directories;

internal class CompareDirectoriesSource(string firstDirectory, string secondDirectory, RuntimeContext runtimeContext)
    : RowSourceBase<CompareDirectoriesResult>
{
    private readonly DirectoryInfo _firstDirectory = new(firstDirectory);
    private readonly DirectoryInfo _secondDirectory = new(secondDirectory);

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var leftJoinedFiles = from firstDirFile in GetAllFiles(_firstDirectory)
            join secondDirFile in GetAllFiles(_secondDirectory) on firstDirFile.FullPath.Replace(_firstDirectory.FullName, string.Empty) equals secondDirFile.FullPath.Replace(_secondDirectory.FullName, string.Empty) into files
            from secondDirFile in files.DefaultIfEmpty()
            select new SourceDestinationFilesPair([firstDirFile, secondDirFile]);

        var rightJoinedFiles = from secondDirFile in GetAllFiles(_secondDirectory)
            where !File.Exists(Path.Combine(_firstDirectory.FullName, secondDirFile.FullPath.Replace(_secondDirectory.FullName, string.Empty).Trim('\\')))
            select new SourceDestinationFilesPair([null, secondDirFile]);

        var allFiles = leftJoinedFiles.Concat(rightJoinedFiles);

        var lib = new OsLibrary();
        var source = new List<IObjectResolver>();

        foreach(var files in allFiles)
        {
            State result;

            // 11
            if (files.Source != null && files.Destination != null)
            {
                result = lib.Sha256File(files.Source) != lib.Sha256File(files.Destination) ? State.Modified : State.TheSame;
            }
            // 10
            else if (files.Source != null)
            {
                result = State.Removed;
            }
            // 01
            else if (files.Destination != null)
            {
                result = State.Added;
            }
            // 00
            else
            {
                continue;
            }

            var value = new CompareDirectoriesResult(_firstDirectory, files.Source, _secondDirectory, files.Destination, result);

            source.Add(new EntityResolver<CompareDirectoriesResult>(value, CompareDirectoriesHelper.CompareDirectoriesNameToIndexMap, CompareDirectoriesHelper.CompareDirectoriesIndexToMethodAccessMap));

            if (source.Count <= 100)
            {
                continue;
            }

            runtimeContext.EndWorkToken.ThrowIfCancellationRequested();

            chunkedSource.Add(source);
            source = [];
        }

        if (source.Count > 0)
            chunkedSource.Add(source);
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
using System.Linq;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Directories;

internal sealed class DirectoryEntitiesSource(
    string path,
    bool recursive,
    SourceExecutionContext executionContext) : RowSourceBase<DirectoryEntity>
{
    protected override void CollectChunks(IChunkWriter<DirectoryEntity> writer)
    {
        var source = new DirectoriesSource(path, recursive, executionContext);

        foreach (var chunk in source.Chunks)
        {
            writer.CancellationToken.ThrowIfCancellationRequested();
            writer.Write(chunk.Select(static directory => new DirectoryEntity(directory)).ToArray());
        }
    }
}

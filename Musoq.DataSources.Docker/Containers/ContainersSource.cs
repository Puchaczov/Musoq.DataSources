using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Docker.Containers;

internal class ContainersSource(IDockerApi api, SourceExecutionContext executionContext)
    : RowSourceBase<ContainerEntity>
{
    private const string ContainersSourceName = "docker_containers";

    protected override void CollectChunks(IChunkWriter<ContainerEntity> writer)
    {
        executionContext.ReportDataSourceBegin(ContainersSourceName);

        try
        {
            var containers = api.ListContainersAsync().Result;
            var rows = containers
                .Select(container => new ContainerEntity(container))
                .Where(entity => DockerSourcePlanner.Matches(executionContext.Plan.AcceptedPredicate, entity))
                .ToList();

            executionContext.ReportDataSourceRowsKnown(ContainersSourceName, rows.Count);
            writer.Write(rows);

            executionContext.ReportDataSourceEnd(ContainersSourceName, rows.Count);
        }
        catch
        {
            executionContext.ReportDataSourceEnd(ContainersSourceName, 0);
            throw;
        }
    }
}

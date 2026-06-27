using Docker.DotNet.Models;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Docker.Containers;

internal class ContainersSource(IDockerApi api, SourceExecutionContext executionContext)
    : RowSourceBase<ContainerListResponse>
{
    private const string ContainersSourceName = "docker_containers";

    protected override void CollectChunks(IChunkWriter<ContainerListResponse> writer)
    {
        executionContext.ReportDataSourceBegin(ContainersSourceName);

        try
        {
            var containers = api.ListContainersAsync().Result;
            executionContext.ReportDataSourceRowsKnown(ContainersSourceName, containers.Count);

            writer.Write(containers.ToList());

            executionContext.ReportDataSourceEnd(ContainersSourceName, containers.Count);
        }
        catch
        {
            executionContext.ReportDataSourceEnd(ContainersSourceName, 0);
            throw;
        }
    }
}

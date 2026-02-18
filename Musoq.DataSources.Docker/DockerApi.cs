using Docker.DotNet;
using Docker.DotNet.Models;

namespace Musoq.DataSources.Docker;

internal class DockerApi : IDockerApi
{
    private readonly DockerClient _client;

    public DockerApi(DockerClient client)
    {
        _client = client;
    }

    public Task<IList<ContainerListResponse>> ListContainersAsync()
    {
        return _client.Containers.ListContainersAsync(new ContainersListParameters());
    }

    public Task<IList<ImagesListResponse>> ListImagesAsync()
    {
        return _client.Images.ListImagesAsync(new ImagesListParameters());
    }

    public Task<IList<NetworkResponse>> ListNetworksAsync()
    {
        return _client.Networks.ListNetworksAsync(new NetworksListParameters());
    }

    public async Task<IList<VolumeResponse>> ListVolumesAsync()
    {
        var volumes = await _client.Volumes.ListAsync();

        return volumes.Volumes;
    }
}
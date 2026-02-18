using Docker.DotNet.Models;

namespace Musoq.DataSources.Docker;

internal interface IDockerApi
{
    Task<IList<ContainerListResponse>> ListContainersAsync();

    Task<IList<ImagesListResponse>> ListImagesAsync();

    Task<IList<NetworkResponse>> ListNetworksAsync();

    Task<IList<VolumeResponse>> ListVolumesAsync();
}
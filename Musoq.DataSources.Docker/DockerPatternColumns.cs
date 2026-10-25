#nullable enable
using System;
using Musoq.DataSources.Common;

namespace Musoq.DataSources.Docker;

/// <summary>Reviewed source-row string getters. These never traverse child tables or perform API calls.</summary>
internal static class DockerPatternColumns
{
    internal static readonly PatternColumnBindings<Containers.ContainerEntity> ContainerEntity = new(
        (nameof(Containers.ContainerEntity.ID), static row => row.ID),
        (nameof(Containers.ContainerEntity.Image), static row => row.Image),
        (nameof(Containers.ContainerEntity.ImageID), static row => row.ImageID),
        (nameof(Containers.ContainerEntity.Command), static row => row.Command),
        (nameof(Containers.ContainerEntity.State), static row => row.State),
        (nameof(Containers.ContainerEntity.Status), static row => row.Status),
        (nameof(Containers.ContainerEntity.NetworkSettings), static row => row.NetworkSettings),
        (nameof(Containers.ContainerEntity.FlattenPorts), static row => row.FlattenPorts)
    );

    internal static readonly PatternColumnBindings<Images.ImageEntity> ImageEntity = new(
        (nameof(Images.ImageEntity.ID), static row => row.ID),
        (nameof(Images.ImageEntity.ParentID), static row => row.ParentID)
    );

    internal static readonly PatternColumnBindings<Networks.NetworkEntity> NetworkEntity = new(
        (nameof(Networks.NetworkEntity.Name), static row => row.Name),
        (nameof(Networks.NetworkEntity.ID), static row => row.ID),
        (nameof(Networks.NetworkEntity.Scope), static row => row.Scope),
        (nameof(Networks.NetworkEntity.Driver), static row => row.Driver),
        (nameof(Networks.NetworkEntity.IPAM), static row => row.IPAM),
        (nameof(Networks.NetworkEntity.ConfigFrom), static row => row.ConfigFrom)
    );

    internal static readonly PatternColumnBindings<Volumes.VolumeEntity> VolumeEntity = new(
        (nameof(Volumes.VolumeEntity.CreatedAt), static row => row.CreatedAt),
        (nameof(Volumes.VolumeEntity.Driver), static row => row.Driver),
        (nameof(Volumes.VolumeEntity.Mountpoint), static row => row.Mountpoint),
        (nameof(Volumes.VolumeEntity.Name), static row => row.Name),
        (nameof(Volumes.VolumeEntity.Scope), static row => row.Scope),
        (nameof(Volumes.VolumeEntity.UsageData), static row => row.UsageData)
    );

    internal static IPatternColumnBindings? Get(string name) => name.ToLowerInvariant() switch
    {
        "containers" => ContainerEntity,
        "images" => ImageEntity,
        "networks" => NetworkEntity,
        "volumes" => VolumeEntity,
        _ => null
    };
}

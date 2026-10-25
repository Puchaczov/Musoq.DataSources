#nullable enable
using Docker.DotNet.Models;
using Musoq.DataSources.Docker.Containers;
using Musoq.DataSources.Docker.Images;
using Musoq.DataSources.Docker.Networks;
using Musoq.DataSources.Docker.Volumes;
using Musoq.DataSources.Common;

namespace Musoq.DataSources.Docker;

internal static class DockerNativePatterns
{
    internal static readonly PatternColumnBindings<ContainerListResponse> Container = new(
        ("ID", static row => ContainerEntity.ReadID(row)),
        ("Image", static row => ContainerEntity.ReadImage(row)),
        ("ImageID", static row => ContainerEntity.ReadImageID(row)),
        ("Command", static row => ContainerEntity.ReadCommand(row)),
        ("State", static row => ContainerEntity.ReadState(row)),
        ("Status", static row => ContainerEntity.ReadStatus(row)),
        ("NetworkSettings", static row => ContainerEntity.ReadNetworkSettings(row)),
        ("FlattenPorts", static row => ContainerEntity.ReadFlattenPorts(row))
    );

    internal static readonly PatternColumnBindings<ImagesListResponse> Image = new(
        ("ID", static row => ImageEntity.ReadID(row)),
        ("ParentID", static row => ImageEntity.ReadParentID(row))
    );

    internal static readonly PatternColumnBindings<NetworkResponse> Network = new(
        ("Name", static row => NetworkEntity.ReadName(row)),
        ("ID", static row => NetworkEntity.ReadID(row)),
        ("Scope", static row => NetworkEntity.ReadScope(row)),
        ("Driver", static row => NetworkEntity.ReadDriver(row)),
        ("IPAM", static row => NetworkEntity.ReadIPAM(row)),
        ("ConfigFrom", static row => NetworkEntity.ReadConfigFrom(row))
    );

    internal static readonly PatternColumnBindings<VolumeResponse> Volume = new(
        ("CreatedAt", static row => VolumeEntity.ReadCreatedAt(row)),
        ("Driver", static row => VolumeEntity.ReadDriver(row)),
        ("Mountpoint", static row => VolumeEntity.ReadMountpoint(row)),
        ("Name", static row => VolumeEntity.ReadName(row)),
        ("Scope", static row => VolumeEntity.ReadScope(row)),
        ("UsageData", static row => VolumeEntity.ReadUsageData(row))
    );
}

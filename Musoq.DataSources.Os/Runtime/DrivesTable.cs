namespace Musoq.DataSources.Os.Runtime;

internal sealed class DrivesTable()
    : RuntimeDiscoveryTableBase<DriveEntity>(RuntimeDiscoverySchema.DriveColumns);

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Runtime;

internal sealed class DrivesSource(SourceExecutionContext executionContext)
    : RuntimeDiscoverySourceBase<DriveEntity>(executionContext, "drives")
{
    protected override IEnumerable<DriveEntity> GetRows()
    {
        return DriveInfo.GetDrives()
            .OrderBy(static drive => drive.Name)
            .Select(static drive => new DriveEntity(drive));
    }
}

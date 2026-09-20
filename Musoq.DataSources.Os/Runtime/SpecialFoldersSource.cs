using System;
using System.Collections.Generic;
using System.Linq;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Runtime;

internal sealed class SpecialFoldersSource(SourceExecutionContext executionContext)
    : RuntimeDiscoverySourceBase<SpecialFolderEntity>(executionContext, "specialfolders")
{
    protected override IEnumerable<SpecialFolderEntity> GetRows()
    {
        return Enum.GetNames<Environment.SpecialFolder>()
            .OrderBy(static name => name)
            .Select(static name => new SpecialFolderEntity(
                name,
                Enum.Parse<Environment.SpecialFolder>(name)));
    }
}

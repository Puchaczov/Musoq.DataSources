using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Runtime;

internal sealed class FileAttributesSource(SourceExecutionContext executionContext)
    : RuntimeDiscoverySourceBase<FileAttributeEntity>(executionContext, "fileattributes")
{
    protected override IEnumerable<FileAttributeEntity> GetRows()
    {
        return Enum.GetValues<FileAttributes>()
            .OrderBy(static attribute => (int)attribute)
            .Select(static attribute => new FileAttributeEntity(attribute));
    }
}

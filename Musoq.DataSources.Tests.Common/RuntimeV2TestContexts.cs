using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using Moq;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Tests.Common;

public static class RuntimeV2TestContexts
{
    public static SourceExecutionContext CreateExecutionContext(
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<ISchemaColumn> allColumns = null,
        IReadOnlyDictionary<string, string> sourceRuntimeSettings = null,
        ILogger logger = null,
        SourceExecutionPlan executionPlan = null)
    {
        return new SourceExecutionContext(
            "test",
            executionPlan ?? new SourceExecutionPlan
            {
                Identity = new SourceIdentity("test", "test", "test", "test")
            },
            cancellationToken,
            allColumns ?? [],
            sourceRuntimeSettings ?? new Dictionary<string, string>(),
            logger ?? new Mock<ILogger>().Object,
            null,
            null);
    }
}

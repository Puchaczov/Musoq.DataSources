using System.Collections.Generic;
using System.Linq;
using Musoq.Schema;

namespace Musoq.DataSources.Tests.Common;

public sealed class DataSourceProgressCapture
{
    private readonly List<DataSourceEventArgs> _events = [];

    public DataSourceEventHandler Handler => (_, args) => _events.Add(args);

    public IReadOnlyList<DataSourceEventArgs> Events => _events;

    public IReadOnlyList<DataSourceEventArgs> For(string dataSourceName, DataSourcePhase phase)
    {
        return _events
            .Where(args => args.DataSourceName == dataSourceName && args.Phase == phase)
            .ToArray();
    }
}

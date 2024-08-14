using System.Collections.Concurrent;
using System.Collections.Generic;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Os.Process;

internal class ProcessesSource(RuntimeContext communicator) : RowSourceBase<System.Diagnostics.Process>
{
    protected override void CollectChunks(
        BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var list = new List<EntityResolver<System.Diagnostics.Process>>();
        var endWorkToken = communicator.EndWorkToken;
        var i = 0;
        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            i += 1;
            list.Add(new EntityResolver<System.Diagnostics.Process>(process, ProcessHelper.ProcessNameToIndexMap,
                ProcessHelper.ProcessIndexToMethodAccessMap));

            if (i < 20)
                continue;

            i = 0;
            chunkedSource.Add(list, endWorkToken);
            list = [];
        }

        chunkedSource.Add(list, endWorkToken);
    }
}
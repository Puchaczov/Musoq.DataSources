using System.Collections.Generic;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Json.Tests;

/// <summary>Preserves direct scan observations while materializing concrete fields.</summary>
internal sealed class NativeJsonFixtureSource : RowSource<NativeQueryRowFixtures.FixtureRow>
{
    private readonly RowSource<NativeQueryRowFixtures.FixtureRow> _source;

    public NativeJsonFixtureSource(string path, SourceExecutionContext context)
    {
        var snapshot = JsonSchemaDiscovery.GetSnapshot(path);
        var shape = JsonRowLayout.Create(snapshot, context).CreateNativeShape();
        _source = NativeQueryRowFixtures.Open(new JsonSchema(), "file", path, context, shape);
    }

    public override IEnumerable<IReadOnlyList<NativeQueryRowFixtures.FixtureRow>> Chunks => _source.Chunks;
}

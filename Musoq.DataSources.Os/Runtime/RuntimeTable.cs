namespace Musoq.DataSources.Os.Runtime;

internal sealed class RuntimeTable()
    : RuntimeDiscoveryTableBase<RuntimeEntity>(RuntimeDiscoverySchema.RuntimeColumns);

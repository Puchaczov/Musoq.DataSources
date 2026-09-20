namespace Musoq.DataSources.Os.Runtime;

internal sealed class CurrentCultureTable()
    : RuntimeDiscoveryTableBase<CurrentCultureEntity>(RuntimeDiscoverySchema.CurrentCultureColumns);

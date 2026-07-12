namespace Musoq.DataSources.Os.Runtime;

internal sealed class EnvironmentVariablesTable()
    : RuntimeDiscoveryTableBase<EnvironmentVariableEntity>(RuntimeDiscoverySchema.EnvironmentVariableColumns);

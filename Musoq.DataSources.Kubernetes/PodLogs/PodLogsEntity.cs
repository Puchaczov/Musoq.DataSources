#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Musoq.DataSources.Kubernetes.PodLogs;

public class PodLogsEntity
{
    public string Namespace { get; init; }

    public string Name { get; init; }

    public string ContainerName { get; init; }

    public string Line { get; init; }
}
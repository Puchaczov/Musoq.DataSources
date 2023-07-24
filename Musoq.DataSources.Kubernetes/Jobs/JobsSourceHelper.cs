using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Jobs;

internal static class JobsSourceHelper
{
    internal static readonly IDictionary<string, int> JobsNameToIndexMap = new Dictionary<string, int>
    {
        {nameof(JobEntity.Namespace), 0},
        {nameof(JobEntity.Name), 1},
        {nameof(JobEntity.Completions), 2},
        {nameof(JobEntity.Duration), 3},
        {nameof(JobEntity.Images), 4},
        {nameof(JobEntity.Age), 5}
    };

    internal static readonly IDictionary<int, Func<JobEntity, object?>> JobsIndexToMethodAccessMap = new Dictionary<int, Func<JobEntity, object?>>
    {
        {0, c => c.Namespace},
        {1, c => c.Name},
        {2, c => c.Completions},
        {3, c => c.Duration},
        {4, c => c.Images},
        {5, c => c.Age}
    };
    
    internal static readonly ISchemaColumn[] JobsColumns = {
        new SchemaColumn(nameof(JobEntity.Namespace), 0, typeof(string)),
        new SchemaColumn(nameof(JobEntity.Name), 1, typeof(string)),
        new SchemaColumn(nameof(JobEntity.Completions), 2, typeof(int)),
        new SchemaColumn(nameof(JobEntity.Duration), 3, typeof(TimeSpan?)),
        new SchemaColumn(nameof(JobEntity.Images), 4, typeof(string)),
        new SchemaColumn(nameof(JobEntity.Age), 5, typeof(DateTime?))
    };
}
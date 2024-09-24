using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.CronJobs;

internal static class CronJobsSourceHelper
{
    internal static readonly IReadOnlyDictionary<string, int> CronJobsNameToIndexMap = new Dictionary<string, int>
    {
        {nameof(CronJobEntity.Namespace), 0},
        {nameof(CronJobEntity.Name), 1},
        {nameof(CronJobEntity.Schedule), 2},
        {nameof(CronJobEntity.Statuses), 3},
        {nameof(CronJobEntity.LastScheduleTime), 4}
    };

    internal static readonly IReadOnlyDictionary<int, Func<CronJobEntity, object?>> CronJobsIndexToMethodAccessMap =
        new Dictionary<int, Func<CronJobEntity, object?>>
        {
            {0, cj => cj.Namespace},
            {1, cj => cj.Name},
            {2, cj => cj.Schedule},
            {3, cj => cj.Statuses},
            {4, cj => cj.LastScheduleTime}
        };

    internal static readonly ISchemaColumn[] CronJobsColumns =
    [
        new SchemaColumn(nameof(CronJobEntity.Namespace), 0, typeof(string)),
        new SchemaColumn(nameof(CronJobEntity.Name), 1, typeof(string)),
        new SchemaColumn(nameof(CronJobEntity.Schedule), 2, typeof(string)),
        new SchemaColumn(nameof(CronJobEntity.Statuses), 3, typeof(string)),
        new SchemaColumn(nameof(CronJobEntity.LastScheduleTime), 4, typeof(DateTime?))
    ];
}
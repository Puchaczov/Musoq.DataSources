using k8s.Models;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Events;

internal class EventsSourceHelper
{
    public static readonly IReadOnlyDictionary<string, int> EventsNameToIndexMap = new Dictionary<string, int>
    {
        {nameof(EventEntity.Action), 0},
        {nameof(EventEntity.ApiVersion), 1},
        {nameof(EventEntity.Count), 2},
        {nameof(EventEntity.EventTime), 3},
        {nameof(EventEntity.FirstTimestamp), 4},
        {nameof(EventEntity.InvolvedObject), 5},
        {nameof(EventEntity.Kind), 6},
        {nameof(EventEntity.LastTimestamp), 7},
        {nameof(EventEntity.Message), 8},
        {nameof(EventEntity.Metadata), 9},
        {nameof(EventEntity.Reason), 10},
        {nameof(EventEntity.Related), 11},
        {nameof(EventEntity.ReportingComponent), 12},
        {nameof(EventEntity.ReportingInstance), 13},
        {nameof(EventEntity.Series), 14},
        {nameof(EventEntity.Source), 15},
        {nameof(EventEntity.Type), 16}
    }; 

    public static readonly IReadOnlyDictionary<int, Func<EventEntity, object?>> EventsIndexToMethodAccessMap =
        new Dictionary<int, Func<EventEntity, object?>>
        {
            {0, info => info.Action},
            {1, info => info.ApiVersion},
            {2, info => info.Count},
            {3, info => info.EventTime},
            {4, info => info.FirstTimestamp},
            {5, info => info.InvolvedObject},
            {6, info => info.Kind},
            {7, info => info.LastTimestamp},
            {8, info => info.Message},
            {9, info => info.Metadata},
            {10, info => info.Reason},
            {11, info => info.Related},
            {12, info => info.ReportingComponent},
            {13, info => info.ReportingInstance},
            {14, info => info.Series},
            {15, info => info.Source},
            {16, info => info.Type}
        };
    
    public static readonly ISchemaColumn[] EventsColumns =
    [
        new SchemaColumn(nameof(EventEntity.Action), 0, typeof(string)),
        new SchemaColumn(nameof(EventEntity.ApiVersion), 1, typeof(string)),
        new SchemaColumn(nameof(EventEntity.Count), 2, typeof(int?)),
        new SchemaColumn(nameof(EventEntity.EventTime), 3, typeof(System.DateTime?)),
        new SchemaColumn(nameof(EventEntity.FirstTimestamp), 4, typeof(System.DateTime?)),
        new SchemaColumn(nameof(EventEntity.InvolvedObject), 5, typeof(V1ObjectReference)),
        new SchemaColumn(nameof(EventEntity.Kind), 6, typeof(string)),
        new SchemaColumn(nameof(EventEntity.LastTimestamp), 7, typeof(System.DateTime?)),
        new SchemaColumn(nameof(EventEntity.Message), 8, typeof(string)),
        new SchemaColumn(nameof(EventEntity.Metadata), 9, typeof(V1ObjectMeta)),
        new SchemaColumn(nameof(EventEntity.Reason), 10, typeof(string)),
        new SchemaColumn(nameof(EventEntity.Related), 11, typeof(V1ObjectReference)),
        new SchemaColumn(nameof(EventEntity.ReportingComponent), 12, typeof(string)),
        new SchemaColumn(nameof(EventEntity.ReportingInstance), 13, typeof(string)),
        new SchemaColumn(nameof(EventEntity.Series), 14, typeof(Corev1EventSeries)),
        new SchemaColumn(nameof(EventEntity.Source), 15, typeof(V1EventSource)),
        new SchemaColumn(nameof(EventEntity.Type), 16, typeof(string))
    ];
}
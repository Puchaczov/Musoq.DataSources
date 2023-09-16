using System.Collections.Concurrent;
using k8s.Models;
using Musoq.DataSources.Kubernetes.Deployments;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Kubernetes.Events;

internal class EventsSource : RowSourceBase<DeploymentEntity>
{
    private readonly IKubernetesApi _client;
    private readonly Func<IKubernetesApi, Corev1EventList> _retrieve;

    public EventsSource(IKubernetesApi client, Func<IKubernetesApi, Corev1EventList> retrieve)
    {
        _client = client;
        _retrieve = retrieve;
    }

    protected override void CollectChunks(BlockingCollection<IReadOnlyList<IObjectResolver>> chunkedSource)
    {
        var eventsList = _retrieve(_client);
        
        chunkedSource.Add(
            eventsList.Items.Select(c => 
                new EntityResolver<EventEntity>(MapV1EventToEventEntity(c), EventsSourceHelper.EventsNameToIndexMap, EventsSourceHelper.EventsIndexToMethodAccessMap)).ToList());
    }

    private static EventEntity MapV1EventToEventEntity(Corev1Event corev1Event)
    {
        return new EventEntity
        {
            Action = corev1Event.Action,
            ApiVersion = corev1Event.ApiVersion,
            Count = corev1Event.Count,
            EventTime = corev1Event.EventTime,
            FirstTimestamp = corev1Event.FirstTimestamp,
            InvolvedObject = corev1Event.InvolvedObject,
            Kind = corev1Event.Kind,
            LastTimestamp = corev1Event.LastTimestamp,
            Message = corev1Event.Message,
            Metadata = corev1Event.Metadata,
            Reason = corev1Event.Reason,
            Related = corev1Event.Related,
            ReportingComponent = corev1Event.ReportingComponent,
            ReportingInstance = corev1Event.ReportingInstance,
            Series = corev1Event.Series,
            Source = corev1Event.Source,
            Type = corev1Event.Type
        };
    }
}
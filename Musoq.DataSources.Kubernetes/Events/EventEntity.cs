using k8s.Models;

namespace Musoq.DataSources.Kubernetes.Events;

public class EventEntity
{
    public string Action { get; set; }

    public string ApiVersion { get; set; }

    public int? Count { get; set; }

    public System.DateTime? EventTime { get; set; }

    public System.DateTime? FirstTimestamp { get; set; }

    public V1ObjectReference InvolvedObject { get; set; }

    public string Kind { get; set; }

    public System.DateTime? LastTimestamp { get; set; }

    public string Message { get; set; }
    
    public V1ObjectMeta Metadata { get; set; }

    public string Reason { get; set; }

    public V1ObjectReference Related { get; set; }

    public string ReportingComponent { get; set; }

    public string ReportingInstance { get; set; }

    public Corev1EventSeries Series { get; set; }

    public V1EventSource Source { get; set; }

    public string Type { get; set; }
}
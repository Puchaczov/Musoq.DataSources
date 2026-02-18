using k8s.Models;

namespace Musoq.DataSources.Kubernetes.Services;

public class ServiceEntity
{
    public V1ObjectMeta Metadata { get; set; }

    public V1ServiceSpec Spec { get; set; }

    public string Kind { get; set; }

    public V1ServiceStatus Status { get; set; }
}
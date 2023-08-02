using k8s.Models;

namespace Musoq.DataSources.Kubernetes;

public interface IWithObjectMetadata
{
    V1ObjectMeta Metadata { get; }
}
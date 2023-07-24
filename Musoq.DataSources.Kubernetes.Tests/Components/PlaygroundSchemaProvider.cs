using Musoq.Schema;

namespace Musoq.DataSources.Kubernetes.Tests.Components;

public class PlaygroundSchemaProvider : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        return new KubernetesSchema();
    }
}
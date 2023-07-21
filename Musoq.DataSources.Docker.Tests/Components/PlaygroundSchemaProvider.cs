using Musoq.Schema;

namespace Musoq.DataSources.Docker.Tests.Components;

public class PlaygroundSchemaProvider : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        return new DockerSchema();
    }
}
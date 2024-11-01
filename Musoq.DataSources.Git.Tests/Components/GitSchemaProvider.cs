using Musoq.Schema;

namespace Musoq.DataSources.Git.Tests.Components;

public class GitSchemaProvider : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        return new GitSchema();
    }
}
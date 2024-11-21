using Musoq.DataSources.Git;
using Musoq.DataSources.Os;
using Musoq.Schema;

namespace Musoq.DataSources.OsAndGitTests.Components;

public class OsAndGitSchemaProvider : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        switch (schema.ToLowerInvariant())
        {
            case "#git":
                return new GitSchema();
            case "#os":
                return new OsSchema();
        }

        throw new Exception("Schema not found");
    }
}
using Musoq.Schema;

namespace Musoq.DataSources.Roslyn.Tests;

public class RoslynSchemaProvider : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        CSharpLifecycleHooks.LoadRequiredDependencies();
        return new CSharpSchema();
    }
}
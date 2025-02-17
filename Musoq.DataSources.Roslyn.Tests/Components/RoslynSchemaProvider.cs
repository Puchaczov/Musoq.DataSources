using Musoq.Schema;

namespace Musoq.DataSources.Roslyn.Tests.Components;

public class RoslynSchemaProvider : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        LifecycleHooks.LoadRequiredDependencies();
        
        return new CSharpSchema();
    }
}
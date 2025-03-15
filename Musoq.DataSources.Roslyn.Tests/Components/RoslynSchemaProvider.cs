using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.Schema;

namespace Musoq.DataSources.Roslyn.Tests.Components;

public class RoslynSchemaProvider(INuGetPropertiesResolver aiBasedPropertiesResolver) : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        LifecycleHooks.LoadRequiredDependencies();
        
        return new CSharpSchema(aiBasedPropertiesResolver);
    }
}
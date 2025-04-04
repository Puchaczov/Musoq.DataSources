using Musoq.DataSources.Roslyn.Components;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.Schema;

namespace Musoq.DataSources.Roslyn.Tests.Components;

public class RoslynSchemaProvider(Func<string, IHttpClient, INuGetPropertiesResolver> createNugetPropertiesResolver) : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        LifecycleHooks.LoadRequiredDependencies();
        
        return new CSharpSchema(createNugetPropertiesResolver);
    }
}
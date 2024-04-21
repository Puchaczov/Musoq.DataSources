using Musoq.Schema;

namespace Musoq.DataSources.Ollama.Tests.Components;

internal class TestsOllamaSchemaProvider : ISchemaProvider
{
    private readonly IOllamaApi _ollamaApi;

    public TestsOllamaSchemaProvider(IOllamaApi ollamaApi)
    {
        _ollamaApi = ollamaApi;
    }

    public ISchema GetSchema(string schema)
    {
        return new TestsOllamaSchema(_ollamaApi);
    }
}
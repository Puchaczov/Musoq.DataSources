using System;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Ollama.Tests.Components;

internal class TestsOllamaSchema : OllamaSchema
{
    private readonly IOllamaApi _ollamaApi;

    public TestsOllamaSchema(IOllamaApi ollamaApi)
    {
        _ollamaApi = ollamaApi;
    }

    public ISchemaTable[] GetSchema()
    {
        return new ISchemaTable[]
        {
            new OllamaSingleRowTable()
        };
    }

    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return new TestsOllamaSingleRowSource(_ollamaApi, new OllamaRequestInfo
        {
            Model = parameters.Length > 0 ? Convert.ToString(parameters[0]) ?? "test-model" : "test-model",
            Temperature = parameters.Length > 1 ? Convert.ToSingle(parameters[1]) : 0
        });
    }
}
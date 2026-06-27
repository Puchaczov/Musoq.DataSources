using System;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

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
        return
        [
            new OllamaSingleRowTable()
        ];
    }

    public override RowSource<T> GetRowSource<T>(
        string name,
        SourceExecutionContext executionContext,
        params object[] parameters)
    {
        return EnsureSourceType<T, OllamaEntity>(
            name,
            new TestsOllamaSingleRowSource(_ollamaApi, new OllamaRequestInfo
            {
                Model = parameters.Length > 0 ? Convert.ToString(parameters[0]) ?? "test-model" : "test-model",
                Temperature = parameters.Length > 1 ? Convert.ToSingle(parameters[1]) : 0
            }));
    }
}

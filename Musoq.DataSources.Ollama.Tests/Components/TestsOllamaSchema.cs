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

    public override RowSource<T> GetRowSource<T, TArguments>(
        string name,
        SourceExecutionContext executionContext,
        TArguments parameters)
    {
        return EnsureSourceType<T, OllamaEntity>(
            name,
            new TestsOllamaSingleRowSource(_ollamaApi, new OllamaRequestInfo
            {
                Model = parameters.Count > 0 ? parameters.GetValue<string>(0) ?? "test-model" : "test-model",
                Temperature = parameters.Count > 1 ? (float)parameters.GetValue<decimal>(1) : 0
            }));
    }
}

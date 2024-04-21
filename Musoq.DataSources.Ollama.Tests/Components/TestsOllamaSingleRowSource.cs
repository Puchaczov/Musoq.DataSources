using System;
using Musoq.Schema;

namespace Musoq.DataSources.Ollama.Tests.Components;

internal class TestsOllamaSingleRowSource : OllamaSingleRowSource
{
    public TestsOllamaSingleRowSource(IOllamaApi openAiApi, OllamaRequestInfo openAiRequestInfo) 
        : base(openAiApi, openAiRequestInfo)
    {
    }

    public TestsOllamaSingleRowSource(RuntimeContext runtimeContext, OllamaRequestInfo ollamaRequestInfo) 
        : base(runtimeContext, ollamaRequestInfo)
    {
        throw new NotImplementedException();
    }
}
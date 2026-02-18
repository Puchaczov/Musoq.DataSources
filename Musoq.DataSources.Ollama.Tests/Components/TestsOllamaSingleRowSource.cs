using System;
using System.Net.Http;
using Musoq.Schema;

namespace Musoq.DataSources.Ollama.Tests.Components;

internal class TestsOllamaSingleRowSource : OllamaSingleRowSource
{
    public TestsOllamaSingleRowSource(IOllamaApi openAiApi, OllamaRequestInfo openAiRequestInfo)
        : base(openAiApi, openAiRequestInfo)
    {
    }

    public TestsOllamaSingleRowSource(RuntimeContext runtimeContext, OllamaRequestInfo ollamaRequestInfo,
        IHttpClientFactory httpClientFactory)
        : base(runtimeContext, ollamaRequestInfo, httpClientFactory)
    {
        throw new NotImplementedException();
    }
}
using Musoq.Schema.Optimization;
namespace Musoq.DataSources.Ollama.Tests.Components;

internal class TestsOllamaSingleRowSource : OllamaSingleRowSource
{
    public TestsOllamaSingleRowSource(IOllamaApi openAiApi, OllamaRequestInfo openAiRequestInfo, SourceExecutionContext context = null)
        : base(openAiApi, openAiRequestInfo, context)
    {
    }
}

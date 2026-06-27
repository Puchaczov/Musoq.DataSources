namespace Musoq.DataSources.Ollama.Tests.Components;

internal class TestsOllamaSingleRowSource : OllamaSingleRowSource
{
    public TestsOllamaSingleRowSource(IOllamaApi openAiApi, OllamaRequestInfo openAiRequestInfo)
        : base(openAiApi, openAiRequestInfo)
    {
    }
}

using Musoq.Schema;

namespace Musoq.DataSources.OpenAI.Tests.Components;

internal class TestsOpenAiSchemaProvider : ISchemaProvider
{
    private readonly IOpenAiApi _openAiApi;

    public TestsOpenAiSchemaProvider(IOpenAiApi openAiApi)
    {
        _openAiApi = openAiApi;
    }

    public ISchema GetSchema(string schema)
    {
        return new TestsOpenAiSchema(_openAiApi);
    }
}
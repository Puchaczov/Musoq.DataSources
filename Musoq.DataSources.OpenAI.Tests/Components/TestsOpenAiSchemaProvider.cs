using Musoq.Schema;

namespace Musoq.DataSources.OpenAI.Tests.Components;

internal class TestsOpenAiSchemaProvider(IOpenAiApi openAiApi) : ISchemaProvider
{
    public ISchema GetSchema(string schema)
    {
        return new TestsOpenAiSchema(openAiApi);
    }
}
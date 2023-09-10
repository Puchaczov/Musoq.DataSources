using Musoq.DataSources.OpenAIHelpers;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.OpenAI;

internal class OpenAiSingleRowSource : RowSource
{
    private readonly IOpenAiApi _openAiApi;
    private readonly OpenAiRequestInfo _openAiRequestInfo;

    protected OpenAiSingleRowSource(IOpenAiApi openAiApi, OpenAiRequestInfo openAiRequestInfo)
    {
        _openAiApi = openAiApi;
        _openAiRequestInfo = openAiRequestInfo;
    }
    
    public OpenAiSingleRowSource(RuntimeContext runtimeContext, OpenAiRequestInfo openAiRequestInfo)
    {
        _openAiApi = new OpenAiApi(runtimeContext.EnvironmentVariables["OPENAI_API_KEY"]);
        _openAiRequestInfo = openAiRequestInfo;
    }

    public override IEnumerable<IObjectResolver> Rows
    {
        get
        {
            return new IObjectResolver[]
            {
                new EntityResolver<OpenAiEntity>(
                    new OpenAiEntity(
                        _openAiApi,
                        _openAiRequestInfo.Model,
                        _openAiRequestInfo.FrequencyPenalty,
                        _openAiRequestInfo.MaxTokens,
                        _openAiRequestInfo.PresencePenalty,
                        _openAiRequestInfo.Temperature),
                    OpenAiSchemaHelper.NameToIndexMap, 
                    OpenAiSchemaHelper.IndexToMethodAccessMap)
            };
        }
    }
}
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.OpenAI;

internal class OpenAiSingleRowSource : RowSource
{
    private readonly IOpenAiApi _openAiApi;
    private readonly OpenAiRequestInfo _openAiRequestInfo;
    private readonly CancellationToken _cancellationToken;

    protected OpenAiSingleRowSource(IOpenAiApi openAiApi, OpenAiRequestInfo openAiRequestInfo, CancellationToken cancellationToken)
    {
        _openAiApi = openAiApi;
        _openAiRequestInfo = openAiRequestInfo;
        _cancellationToken = cancellationToken;
    }
    
    public OpenAiSingleRowSource(RuntimeContext runtimeContext, OpenAiRequestInfo openAiRequestInfo)
    {
        _openAiApi = new OpenAiApi(runtimeContext.EnvironmentVariables["OPENAI_API_KEY"]);
        _openAiRequestInfo = openAiRequestInfo;
        _cancellationToken = runtimeContext.EndWorkToken;
    }

    public override IEnumerable<IObjectResolver> Rows =>
    [
        new EntityResolver<OpenAiEntity>(
            new OpenAiEntity(
                _openAiApi,
                _openAiRequestInfo.Model,
                _openAiRequestInfo.FrequencyPenalty,
                _openAiRequestInfo.MaxTokens,
                _openAiRequestInfo.PresencePenalty,
                _openAiRequestInfo.Temperature,
                _cancellationToken),
            OpenAiSchemaHelper.NameToIndexMap, 
            OpenAiSchemaHelper.IndexToMethodAccessMap)
    ];
}
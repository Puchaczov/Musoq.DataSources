using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Ollama;

internal class OllamaSingleRowSource : RowSource
{
    private readonly IOllamaApi _openAiApi;
    private readonly OllamaRequestInfo _openAiRequestInfo;
    private readonly CancellationToken _cancellationToken;

    protected OllamaSingleRowSource(IOllamaApi openAiApi, OllamaRequestInfo openAiRequestInfo)
    {
        _openAiApi = openAiApi;
        _openAiRequestInfo = openAiRequestInfo;
        _cancellationToken = CancellationToken.None;
    }
    
    public OllamaSingleRowSource(RuntimeContext runtimeContext, OllamaRequestInfo openAiRequestInfo)
    {
        _openAiApi = new OllamaApi(openAiRequestInfo.OllamaBaseUrl);
        _openAiRequestInfo = openAiRequestInfo;
        _cancellationToken = runtimeContext.EndWorkToken;
    }

    public override IEnumerable<IObjectResolver> Rows
    {
        get
        {
            return new IObjectResolver[]
            {
                new EntityResolver<OllamaEntity>(
                    new OllamaEntity(
                        _openAiApi,
                        _openAiRequestInfo.Model,
                        _openAiRequestInfo.Temperature,
                        _cancellationToken),
                    OllamaSchemaHelper.NameToIndexMap, 
                    OllamaSchemaHelper.IndexToMethodAccessMap)
            };
        }
    }
}
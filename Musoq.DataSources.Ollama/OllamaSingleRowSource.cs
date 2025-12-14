using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Ollama;

internal class OllamaSingleRowSource : RowSource
{
    private const string OllamaSourceName = "ollama";
    private readonly IOllamaApi _openAiApi;
    private readonly OllamaRequestInfo _openAiRequestInfo;
    private readonly RuntimeContext? _runtimeContext;

    protected OllamaSingleRowSource(IOllamaApi openAiApi, OllamaRequestInfo openAiRequestInfo)
    {
        _openAiApi = openAiApi;
        _openAiRequestInfo = openAiRequestInfo;
        _runtimeContext = null;
    }
    
    public OllamaSingleRowSource(RuntimeContext runtimeContext, OllamaRequestInfo openAiRequestInfo, IHttpClientFactory httpClientFactory)
    {
        _openAiApi = new OllamaApi(openAiRequestInfo.OllamaBaseUrl, httpClientFactory);
        _openAiRequestInfo = openAiRequestInfo;
        _runtimeContext = runtimeContext;
    }

    public override IEnumerable<IObjectResolver> Rows
    {
        get
        {
            _runtimeContext?.ReportDataSourceBegin(OllamaSourceName);
            _runtimeContext?.ReportDataSourceRowsKnown(OllamaSourceName, 1);
            
            try
            {
                yield return new EntityResolver<OllamaEntity>(
                    new OllamaEntity(
                        _openAiApi,
                        _openAiRequestInfo.Model,
                        _openAiRequestInfo.Temperature,
                        _runtimeContext?.EndWorkToken ?? CancellationToken.None),
                    OllamaSchemaHelper.NameToIndexMap, 
                    OllamaSchemaHelper.IndexToMethodAccessMap);
            }
            finally
            {
                _runtimeContext?.ReportDataSourceEnd(OllamaSourceName, 1);
            }
        }
    }
}
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Ollama;

internal class OllamaSingleRowSource : RowSourceBase<OllamaEntity>
{
    private const string OllamaSourceName = "ollama";
    private readonly IOllamaApi _openAiApi;
    private readonly OllamaRequestInfo _openAiRequestInfo;
    private readonly SourceExecutionContext? _executionContext;

    protected OllamaSingleRowSource(IOllamaApi openAiApi, OllamaRequestInfo openAiRequestInfo)
    {
        _openAiApi = openAiApi;
        _openAiRequestInfo = openAiRequestInfo;
    }

    public OllamaSingleRowSource(
        SourceExecutionContext executionContext,
        OllamaRequestInfo openAiRequestInfo,
        IHttpClientFactory httpClientFactory)
    {
        _openAiApi = new OllamaApi(openAiRequestInfo.OllamaBaseUrl, httpClientFactory);
        _openAiRequestInfo = openAiRequestInfo;
        _executionContext = executionContext;
    }

    protected override void CollectChunks(IChunkWriter<OllamaEntity> writer)
    {
        _executionContext?.ReportDataSourceBegin(OllamaSourceName);
        _executionContext?.ReportDataSourceRowsKnown(OllamaSourceName, 1);

        try
        {
            writer.Write([
                new OllamaEntity(
                    _openAiApi,
                    _openAiRequestInfo.Model,
                    _openAiRequestInfo.Temperature,
                    writer.CancellationToken)
            ]);
        }
        finally
        {
            _executionContext?.ReportDataSourceEnd(OllamaSourceName, 1);
        }
    }
}

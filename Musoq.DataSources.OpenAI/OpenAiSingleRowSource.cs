using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.OpenAI;

internal class OpenAiSingleRowSource : RowSourceBase<OpenAiEntity>
{
    private const string OpenAiSourceName = "openai";
    private readonly IOpenAiApi _openAiApi;
    private readonly OpenAiRequestInfo _openAiRequestInfo;
    private readonly SourceExecutionContext? _executionContext;

    protected OpenAiSingleRowSource(IOpenAiApi openAiApi, OpenAiRequestInfo openAiRequestInfo)
    {
        _openAiApi = openAiApi;
        _openAiRequestInfo = openAiRequestInfo;
    }

    public OpenAiSingleRowSource(SourceExecutionContext executionContext, OpenAiRequestInfo openAiRequestInfo)
    {
        _openAiApi = new OpenAiApi(executionContext.SourceRuntimeSettings["OPENAI_API_KEY"]);
        _openAiRequestInfo = openAiRequestInfo;
        _executionContext = executionContext;
    }

    protected override void CollectChunks(IChunkWriter<OpenAiEntity> writer)
    {
        _executionContext?.ReportDataSourceBegin(OpenAiSourceName);
        _executionContext?.ReportDataSourceRowsKnown(OpenAiSourceName, 1);

        try
        {
            writer.Write([
                new OpenAiEntity(
                    _openAiApi,
                    _openAiRequestInfo.Model,
                    _openAiRequestInfo.FrequencyPenalty,
                    _openAiRequestInfo.MaxTokens,
                    _openAiRequestInfo.PresencePenalty,
                    _openAiRequestInfo.Temperature,
                    writer.CancellationToken)
            ]);
        }
        finally
        {
            _executionContext?.ReportDataSourceEnd(OpenAiSourceName, 1);
        }
    }
}

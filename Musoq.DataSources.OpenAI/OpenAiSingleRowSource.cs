using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.OpenAI;

internal class OpenAiSingleRowSource : RowSource
{
    private const string OpenAiSourceName = "openai";
    private readonly IOpenAiApi _openAiApi;
    private readonly OpenAiRequestInfo _openAiRequestInfo;
    private readonly RuntimeContext? _runtimeContext;

    protected OpenAiSingleRowSource(IOpenAiApi openAiApi, OpenAiRequestInfo openAiRequestInfo)
    {
        _openAiApi = openAiApi;
        _openAiRequestInfo = openAiRequestInfo;
        _runtimeContext = null;
    }

    public OpenAiSingleRowSource(RuntimeContext runtimeContext, OpenAiRequestInfo openAiRequestInfo)
    {
        _openAiApi = new OpenAiApi(runtimeContext.EnvironmentVariables["OPENAI_API_KEY"]);
        _openAiRequestInfo = openAiRequestInfo;
        _runtimeContext = runtimeContext;
    }

    public override IEnumerable<IObjectResolver> Rows
    {
        get
        {
            _runtimeContext?.ReportDataSourceBegin(OpenAiSourceName);
            _runtimeContext?.ReportDataSourceRowsKnown(OpenAiSourceName, 1);

            try
            {
                yield return new EntityResolver<OpenAiEntity>(
                    new OpenAiEntity(
                        _openAiApi,
                        _openAiRequestInfo.Model,
                        _openAiRequestInfo.FrequencyPenalty,
                        _openAiRequestInfo.MaxTokens,
                        _openAiRequestInfo.PresencePenalty,
                        _openAiRequestInfo.Temperature,
                        _runtimeContext?.EndWorkToken ?? CancellationToken.None),
                    OpenAiSchemaHelper.NameToIndexMap,
                    OpenAiSchemaHelper.IndexToMethodAccessMap);
            }
            finally
            {
                _runtimeContext?.ReportDataSourceEnd(OpenAiSourceName, 1);
            }
        }
    }
}
using System;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using OpenAI_API.Models;

namespace Musoq.DataSources.OpenAI.Tests.Components;

internal class TestsOpenAiSchema : OpenAiSchema
{
    private readonly IOpenAiApi _openAiApi;

    public TestsOpenAiSchema(IOpenAiApi openAiApi)
    {
        _openAiApi = openAiApi;
    }

    public ISchemaTable[] GetSchema()
    {
        return new ISchemaTable[]
        {
            new OpenAiSingleRowTable()
        };
    }

    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return new TestsOpenAiSingleRowSource(_openAiApi, new OpenAiRequestInfo
        {
            Model = parameters.Length > 0 ? Convert.ToString(parameters[0]) ?? Model.DavinciText : Model.DavinciText,
            MaxTokens = parameters.Length > 1 ? Convert.ToInt32(parameters[1]) : 4000,
            Temperature = parameters.Length > 2 ? Convert.ToDouble(parameters[2]) : 0.0,
            FrequencyPenalty = parameters.Length > 3 ? Convert.ToDouble(parameters[3]) : 0.0,
            PresencePenalty = parameters.Length > 4 ? Convert.ToDouble(parameters[4]) : 0.0
        });
    }
}
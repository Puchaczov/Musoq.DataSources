using System;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.OpenAI.Tests.Components;

internal class TestsOpenAiSchema(IOpenAiApi openAiApi) : OpenAiSchema
{
    public ISchemaTable[] GetSchema()
    {
        return
        [
            new OpenAiSingleRowTable()
        ];
    }

    public override RowSource GetRowSource(string name, RuntimeContext runtimeContext, params object[] parameters)
    {
        return new TestsOpenAiSingleRowSource(openAiApi, new OpenAiRequestInfo
        {
            Model = parameters.Length > 0 ? Convert.ToString(parameters[0]) ?? Defaults.DefaultModel : Defaults.DefaultModel,
            MaxTokens = parameters.Length > 1 ? Convert.ToInt32(parameters[1]) : 4000,
            Temperature = parameters.Length > 2 ? Convert.ToSingle(parameters[2]) : 0,
            FrequencyPenalty = parameters.Length > 3 ? Convert.ToSingle(parameters[3]) : 0,
            PresencePenalty = parameters.Length > 4 ? Convert.ToSingle(parameters[4]) : 0
        });
    }
}
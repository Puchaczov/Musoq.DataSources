using System;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

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

    public override RowSource<T> GetRowSource<T, TArguments>(
        string name,
        SourceExecutionContext executionContext,
        TArguments parameters)
    {
        return EnsureSourceType<T, OpenAiEntity>(
            name,
            new TestsOpenAiSingleRowSource(openAiApi, new OpenAiRequestInfo
            {
                Model = parameters.Count > 0
                    ? parameters.GetValue<string>(0) ?? Defaults.DefaultModel
                    : Defaults.DefaultModel,
                MaxTokens = parameters.Count > 1 ? parameters.GetValue<int>(1) : 4000,
                Temperature = parameters.Count > 2 ? parameters.GetValue<float>(2) : 0,
                FrequencyPenalty = parameters.Count > 3 ? parameters.GetValue<float>(3) : 0,
                PresencePenalty = parameters.Count > 4 ? parameters.GetValue<float>(4) : 0
            }));
    }
}

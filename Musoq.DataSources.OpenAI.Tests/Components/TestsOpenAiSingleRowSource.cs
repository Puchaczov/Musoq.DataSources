using System;
using Musoq.DataSources.OpenAIHelpers;
using Musoq.Schema;

namespace Musoq.DataSources.OpenAI.Tests.Components;

internal class TestsOpenAiSingleRowSource : OpenAiSingleRowSource
{
    public TestsOpenAiSingleRowSource(IOpenAiApi openAiApi, OpenAiRequestInfo openAiRequestInfo) 
        : base(openAiApi, openAiRequestInfo)
    {
    }

    public TestsOpenAiSingleRowSource(RuntimeContext runtimeContext, OpenAiRequestInfo openAiRequestInfo) 
        : base(runtimeContext, openAiRequestInfo)
    {
        throw new NotImplementedException();
    }
}
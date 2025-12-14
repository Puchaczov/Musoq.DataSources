using System;
using System.Threading;
using Musoq.Schema;

namespace Musoq.DataSources.OpenAI.Tests.Components;

internal class TestsOpenAiSingleRowSource : OpenAiSingleRowSource
{
    public TestsOpenAiSingleRowSource(IOpenAiApi openAiApi, OpenAiRequestInfo openAiRequestInfo) 
        : base(openAiApi, openAiRequestInfo)
    {
    }
}
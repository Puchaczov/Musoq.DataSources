using System;
using System.Threading;
using Musoq.Schema;

namespace Musoq.DataSources.OpenAI.Tests.Components;

internal class TestsOpenAiSingleRowSource : OpenAiSingleRowSource
{
    public TestsOpenAiSingleRowSource(IOpenAiApi openAiApi, OpenAiRequestInfo openAiRequestInfo, CancellationToken cancellationToken) 
        : base(openAiApi, openAiRequestInfo, cancellationToken)
    {
    }

    public TestsOpenAiSingleRowSource(RuntimeContext runtimeContext, OpenAiRequestInfo openAiRequestInfo) 
        : base(runtimeContext, openAiRequestInfo)
    {
        throw new NotImplementedException();
    }
}
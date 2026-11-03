using Musoq.Schema.Optimization;
﻿namespace Musoq.DataSources.OpenAI.Tests.Components;

internal class TestsOpenAiSingleRowSource : OpenAiSingleRowSource
{
    public TestsOpenAiSingleRowSource(IOpenAiApi openAiApi, OpenAiRequestInfo openAiRequestInfo, SourceExecutionContext context = null)
        : base(openAiApi, openAiRequestInfo, context)
    {
    }
}
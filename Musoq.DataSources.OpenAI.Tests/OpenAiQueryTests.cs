using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.Converter;
using Musoq.DataSources.OpenAI.Tests.Components;
using Musoq.DataSources.OpenAIHelpers;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using OpenAI_API.Chat;
using Environment = Musoq.Plugins.Environment;

namespace Musoq.DataSources.OpenAI.Tests;

[TestClass]
public class OpenAiQueryTests
{
    [TestMethod]
    public void WhenCallingIsContentAbout_ShouldBeTrue()
    {
        const string script = "select IsContentAbout('abc', 'abc') from #openai.gpt()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "yes");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(true, table[0][0]);
    }
    
    [TestMethod]
    public void WhenCallingIsContentAbout_ShouldBeFalse()
    {
        const string script = "select IsContentAbout('abc', 'abc') from #openai.gpt()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "no");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(false, table[0][0]);
    }
    
    [TestMethod]
    public void WhenCallingSentiment_ShouldBePositive()
    {
        const string script = "select Sentiment('abc') from #openai.gpt()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "POSITIVE");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("POSITIVE", table[0][0]);
    }
    
    [TestMethod]
    public void WhenCallingSentiment_ShouldBeNegative()
    {
        const string script = "select Sentiment('abc') from #openai.gpt()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "NEGATIVE");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("NEGATIVE", table[0][0]);
    }
    
    [TestMethod]
    public void WhenCallingSentiment_ShouldBeNeutral()
    {
        const string script = "select Sentiment('abc') from #openai.gpt()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "NEUTRAL");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("NEUTRAL", table[0][0]);
    }
    
    [TestMethod]
    public void WhenCallingSentiment_ShouldBeUnknown()
    {
        const string script = "select Sentiment('abc') from #openai.gpt()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "UNKNOWN");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("UNKNOWN", table[0][0]);
    }
    
    [TestMethod]
    public void WhenCallingSummarizeContent_ShouldBeSummarized()
    {
        const string script = "select SummarizeContent('abc') from #openai.gpt()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "summarized");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("summarized", table[0][0]);
    }
    
    [TestMethod]
    public void WhenCallingTranslateContent_ShouldBeTranslated()
    {
        const string script = "select TranslateContent('abc', 'en', 'pl') from #openai.gpt()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "translated");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("translated", table[0][0]);
    }
    
    [TestMethod]
    public void WhenCallingEntities_ShouldExtractEntities()
    {
        const string script = "select Entities('abc') from #openai.gpt()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "{ entities: ['a', 'b', 'c'] }");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.IsTrue(table[0][0] is string[] entities && entities.Contains("a") && entities.Contains("b") && entities.Contains("c"));
    }

    private static CompiledQuery CreateAndRunVirtualMachineWithResponse(string script, string response)
    {
        return InstanceCreator.CompileForExecution(
            script, 
            Guid.NewGuid().ToString(), 
            new TestsOpenAiSchemaProvider(CreateOpenAiApiMock(response).Object), 
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static Mock<IOpenAiApi> CreateOpenAiApiMock(string response)
    {
        var mockOpenAiApi = new Mock<IOpenAiApi>();

        mockOpenAiApi.Setup(x => x.GetCompletionAsync(It.IsAny<OpenAiEntity>(), It.IsAny<IList<ChatMessage>>()))
            .ReturnsAsync(new ChatResult
            {
                Choices = new[]
                {
                    new ChatChoice()
                    {
                        Message = new ChatMessage(ChatMessageRole.Assistant, response)
                    }
                }
            });
        return mockOpenAiApi;
    }

    static OpenAiQueryTests()
    {
        new Environment().SetValue(Constants.NetStandardDllEnvironmentName, EnvironmentUtils.GetOrCreateEnvironmentVariable());

        Culture.ApplyWithDefaultCulture();
    }
}
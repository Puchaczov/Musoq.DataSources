using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.Converter;
using Musoq.DataSources.Ollama.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using Environment = Musoq.Plugins.Environment;

namespace Musoq.DataSources.Ollama.Tests;

[TestClass]
public class OllamaQueryTests
{
    [TestMethod]
    public void WhenCallingIsContentAbout_ShouldBeTrue()
    {
        const string script = "select IsContentAbout('abc', 'abc') from #ollama.model()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "yes");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(true, table[0][0]);
    }
    
    [TestMethod]
    public void WhenCallingIsContentAbout_ShouldBeFalse()
    {
        const string script = "select IsContentAbout('abc', 'abc') from #ollama.model()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "no");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(false, table[0][0]);
    }
    
    [TestMethod]
    public void WhenCallingSentiment_ShouldBePositive()
    {
        const string script = "select Sentiment('abc') from #ollama.model()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "POSITIVE");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("POSITIVE", table[0][0]);
    }
    
    [TestMethod]
    public void WhenCallingSentiment_ShouldBeNegative()
    {
        const string script = "select Sentiment('abc') from #ollama.model()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "NEGATIVE");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("NEGATIVE", table[0][0]);
    }
    
    [TestMethod]
    public void WhenCallingSentiment_ShouldBeNeutral()
    {
        const string script = "select Sentiment('abc') from #ollama.model()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "NEUTRAL");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("NEUTRAL", table[0][0]);
    }
    
    [TestMethod]
    public void WhenCallingSentiment_ShouldBeUnknown()
    {
        const string script = "select Sentiment('abc') from #ollama.model()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "UNKNOWN");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("UNKNOWN", table[0][0]);
    }
    
    [TestMethod]
    public void WhenCallingSummarizeContent_ShouldBeSummarized()
    {
        const string script = "select SummarizeContent('abc') from #ollama.model()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "summarized");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("summarized", table[0][0]);
    }
    
    [TestMethod]
    public void WhenCallingTranslateContent_ShouldBeTranslated()
    {
        const string script = "select TranslateContent('abc', 'en', 'pl') from #ollama.model()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "translated");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("translated", table[0][0]);
    }
    
    [TestMethod]
    public void WhenCallingEntities_ShouldExtractEntities()
    {
        const string script = "select Entities('abc') from #ollama.model()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "{ entities: ['a', 'b', 'c'] }");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.IsTrue(table[0][0] is string[] entities && entities.Contains("a") && entities.Contains("b") && entities.Contains("c"));
    }
    
    [TestMethod]
    public void WhenCallingLlmPerform_ShouldDoWhateverInstructedFor()
    {
        const string script = "select LlmPerform('extract email from text', 'example email is me@you.com and here is something else') from #ollama.model()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "me@you.com");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("me@you.com", table[0][0]);
    }
    
    [TestMethod]
    public void WhenCallingDescribeImage_ShouldReturnImageDescription()
    {
        const string script = "select DescribeImage('base64OfImage') from #ollama.model()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "image description");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("image description", table[0][0]);
    }
    
    [TestMethod]
    public void WhenCallingAskImage_ShouldReturnResponse()
    {
        const string script = "select AskImage('what color is the water in the picture?', 'base64OfImage') from #ollama.model()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "dirty blue");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("dirty blue", table[0][0]);
    }
    
    [TestMethod]
    public void WhenCallingIsQuestionApplicableToImage_ShouldReturnResponse()
    {
        const string script = "select IsQuestionApplicableToImage('does it contain plane in the background?', 'base64OfImage') from #ollama.model()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, "{ result: true }");
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(true, table[0][0]);
    }
    
    [TestMethod]
    public void WhenCountingTokens_ShouldReturnTokenCount()
    {
        const string script = "select CountTokens('Hello world!') from #ollama.model()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(script, string.Empty);
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);

        Assert.AreEqual(3, table[0][0]);
    }

    private static CompiledQuery CreateAndRunVirtualMachineWithResponse(string script, string response)
    {
        return InstanceCreator.CompileForExecution(
            script, 
            Guid.NewGuid().ToString(), 
            new TestsOllamaSchemaProvider(CreateOpenAiApiMock(response).Object), 
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static Mock<IOllamaApi> CreateOpenAiApiMock(string response)
    {
        var mockOllamaApi = new Mock<IOllamaApi>();

        mockOllamaApi.Setup(x => x.GetCompletionAsync(It.IsAny<OllamaEntity>(), It.IsAny<IList<Message>>()))
            .ReturnsAsync(new ConversationContextWithResponse(response, []));
        mockOllamaApi.Setup(x => x.GetImageCompletionAsync(It.IsAny<OllamaEntity>(), It.IsAny<Message>()))
            .ReturnsAsync(new ConversationContextWithResponse(response, []));
        
        return mockOllamaApi;
    }

    static OllamaQueryTests()
    {
        new Environment().SetValue(Constants.NetStandardDllEnvironmentVariableName, EnvironmentUtils.GetOrCreateEnvironmentVariable());

        Culture.ApplyWithDefaultCulture();
    }
}
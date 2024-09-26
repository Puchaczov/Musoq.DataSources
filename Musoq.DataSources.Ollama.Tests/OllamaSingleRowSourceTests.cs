using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.Schema;
using OllamaSharp;
using OllamaSharp.Models.Chat;

namespace Musoq.DataSources.Ollama.Tests;

[TestClass]
public class OllamaSingleRowSourceTests
{
    [TestMethod]
    public void WhenRowsCalled_ShouldRetrieveSingle()
    {
        var source = new OllamaSingleRowSource(
            new RuntimeContext(
                CancellationToken.None,
                Array.Empty<ISchemaColumn>(),
                new Dictionary<string, string>(),
                (null, null, null, false)), new OllamaRequestInfo
            {
                Model = "test-model",
                Temperature = 0,
                OllamaBaseUrl = OllamaApi.DefaultAddress
            });

        var fired = source.Rows.Count();
        
        Assert.AreEqual(1, fired);
    }

    [TestMethod]
    public void WhenSentimentIsPositive_ShouldPass()
    {
        var mockOpenAiApi = PrepareOpenAiApi("POSITIVE");
        var library = new OllamaLibrary();
        var result = library.Sentiment(
            new OllamaEntity(
                mockOpenAiApi.Object,
                "test-model", 
                0, 
                CancellationToken.None), 
            "NICELY LOOKING SOMETHING");
        
        Assert.AreEqual("POSITIVE", result);
    }

    [TestMethod]
    public void WhenSentimentIsNegative_ShouldPass()
    {
        var mockOpenAiApi = PrepareOpenAiApi("NEGATIVE");
        var library = new OllamaLibrary();
        var result = library.Sentiment(
            new OllamaEntity(
                mockOpenAiApi.Object,
                "test-model", 
                0, 
                CancellationToken.None),
            "BADLY LOOKING SOMETHING");
        
        Assert.AreEqual("NEGATIVE", result);
    }
    
    [TestMethod]
    public void WhenSentimentIsNeutral_ShouldPass()
    {
        var mockOpenAiApi = PrepareOpenAiApi("NEUTRAL");
        var library = new OllamaLibrary();
        var result = library.Sentiment(
            new OllamaEntity(
                mockOpenAiApi.Object,
                "test-model", 
                0, 
                CancellationToken.None), 
            "NEUTRALLY LOOKING SOMETHING");
        
        Assert.AreEqual("NEUTRAL", result);
    }
    
    [TestMethod]
    public void WhenSentimentIsGarbage_ShouldPass()
    {
        var mockOpenAiApi = PrepareOpenAiApi(string.Empty);
        var library = new OllamaLibrary();
        var result = library.Sentiment(
            new OllamaEntity(
                mockOpenAiApi.Object,
                "test-model", 
                0, 
                CancellationToken.None), 
            "some garbage");
        
        Assert.AreEqual("UNKNOWN", result);
    }
    
    [TestMethod]
    public void WhenSummarizeContent_ShouldPass()
    {
        var mockOpenAiApi = PrepareOpenAiApi("SUMMARIZED");
        var library = new OllamaLibrary();
        var result = library.SummarizeContent(
            new OllamaEntity(
                mockOpenAiApi.Object,
                "test-model", 
                0, 
                CancellationToken.None),
            "some content");
        
        Assert.AreEqual("SUMMARIZED", result);
    }
    
    [TestMethod]
    public void WhenIsContentAbout_ShouldPass()
    {
        var mockOpenAiApi = PrepareOpenAiApi("YES");
        var library = new OllamaLibrary();
        var result = library.IsContentAbout(
            new OllamaEntity(
                mockOpenAiApi.Object,
                "test-model", 
                0, 
                CancellationToken.None), 
            "content",
            "question");
        
        Assert.AreEqual(true, result);
    }
    
    [TestMethod]
    public void WhenIsContentNotAbout_ShouldPass()
    {
        var mockOpenAiApi = PrepareOpenAiApi("NO");
        var library = new OllamaLibrary();
        var result = library.IsContentAbout(
            new OllamaEntity(
                mockOpenAiApi.Object,
                "test-model", 
                0, 
                CancellationToken.None), 
            "content",
            "question");
        
        Assert.AreEqual(false, result);
    }
    
    [TestMethod]
    public void WhenTranslateContent_ShouldPass()
    {
        var mockOpenAiApi = PrepareOpenAiApi("translated");
        var library = new OllamaLibrary();
        var result = library.TranslateContent(
            new OllamaEntity(
                mockOpenAiApi.Object,
                "test-model", 
                0, 
                CancellationToken.None), 
            "content",
            "pl",
            "en");
        
        Assert.AreEqual("translated", result);
    }
    
    [TestMethod]
    public void WhenExtractEntities_ShouldPass()
    {
        var mockOpenAiApi = PrepareOpenAiApi("{ \"entities\": [\"extracted\"] }");
        var library = new OllamaLibrary();
        var result = library.Entities(
            new OllamaEntity(
                mockOpenAiApi.Object,
                "test-model", 
                0, 
                CancellationToken.None),
            "content");
        
        Assert.AreEqual(1, result.Length);
        Assert.AreEqual("extracted", result[0]);
    }
    
    [TestMethod]
    public void WhenExtractEntitiesReturnsMalformedJson_ShouldPass()
    {
        var mockOpenAiApi = PrepareOpenAiApi("malformed");
        var library = new OllamaLibrary();
        var result = library.Entities(
            new OllamaEntity(
                mockOpenAiApi.Object,
                "test-model", 
                0, 
                CancellationToken.None), 
            "content");
        
        Assert.AreEqual(0, result.Length);
    }

    private static Mock<IOllamaApi> PrepareOpenAiApi(string response)
    {
        var mock = new Mock<IOllamaApi>();
        mock.Setup(f => f.GetCompletionAsync(
                It.IsAny<OllamaEntity>(),
                It.IsAny<IList<Message>>()))
            .Returns<OllamaEntity, IList<Message>>((entity, messages) =>
                Task.FromResult(
                    new ConversationContextWithResponse(response, [])));
        return mock;
    }
}
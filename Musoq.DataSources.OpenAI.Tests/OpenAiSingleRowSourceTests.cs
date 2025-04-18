﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.Schema;
using OpenAI.Chat;

namespace Musoq.DataSources.OpenAI.Tests;

[TestClass]
public class OpenAiSingleRowSourceTests
{
    private static string ModelName => Defaults.DefaultModel;
    
    [TestMethod]
    public void WhenRowsCalled_ShouldRetrieveSingle()
    {
        var mockLogger = new Mock<ILogger>();
        
        var source = new OpenAiSingleRowSource(
            new RuntimeContext(
                CancellationToken.None,
                Array.Empty<ISchemaColumn>(),
                new Dictionary<string, string>()
                {
                    {"OPENAI_API_KEY", "OPENAI_API_KEY"}
                },
                (null, null, null, false),
                mockLogger.Object), new OpenAiRequestInfo()
            {
                Model = ModelName
            });

        var fired = source.Rows.Count();
        
        Assert.AreEqual(1, fired);
    }

    [TestMethod]
    public void WhenSentimentIsPositive_ShouldPass()
    {
        var mockOpenAiApi = PrepareOpenAiApi("POSITIVE");
        var library = new OpenAiLibrary();
        var result = library.Sentiment(
            new OpenAiEntity(
                mockOpenAiApi.Object, 
                ModelName, 
                0, 
                0, 
                0, 
                0,
                CancellationToken.None), 
            "NICELY LOOKING SOMETHING");
        
        Assert.AreEqual("POSITIVE", result);
    }

    [TestMethod]
    public void WhenSentimentIsNegative_ShouldPass()
    {
        var mockOpenAiApi = PrepareOpenAiApi("NEGATIVE");
        var library = new OpenAiLibrary();
        var result = library.Sentiment(
            new OpenAiEntity(
                mockOpenAiApi.Object, 
                ModelName, 
                0, 
                0, 
                0, 
                0,
                CancellationToken.None), 
            "BADLY LOOKING SOMETHING");
        
        Assert.AreEqual("NEGATIVE", result);
    }
    
    [TestMethod]
    public void WhenSentimentIsNeutral_ShouldPass()
    {
        var mockOpenAiApi = PrepareOpenAiApi("NEUTRAL");
        var library = new OpenAiLibrary();
        var result = library.Sentiment(
            new OpenAiEntity(
                mockOpenAiApi.Object, 
                ModelName, 
                0, 
                0, 
                0, 
                0,
                CancellationToken.None), 
            "NEUTRALLY LOOKING SOMETHING");
        
        Assert.AreEqual("NEUTRAL", result);
    }
    
    [TestMethod]
    public void WhenSentimentIsGarbage_ShouldPass()
    {
        var mockOpenAiApi = PrepareOpenAiApi(string.Empty);
        var library = new OpenAiLibrary();
        var result = library.Sentiment(
            new OpenAiEntity(
                mockOpenAiApi.Object, 
                ModelName, 
                0, 
                0, 
                0, 
                0,
                CancellationToken.None), 
            "some garbage");
        
        Assert.AreEqual("UNKNOWN", result);
    }
    
    [TestMethod]
    public void WhenSummarizeContent_ShouldPass()
    {
        var mockOpenAiApi = PrepareOpenAiApi("SUMMARIZED");
        var library = new OpenAiLibrary();
        var result = library.SummarizeContent(
            new OpenAiEntity(
                mockOpenAiApi.Object, 
                ModelName, 
                0, 
                0, 
                0, 
                0,
                CancellationToken.None), 
            "some content");
        
        Assert.AreEqual("SUMMARIZED", result);
    }
    
    [TestMethod]
    public void WhenIsContentAbout_ShouldPass()
    {
        var mockOpenAiApi = PrepareOpenAiApi("YES");
        var library = new OpenAiLibrary();
        var result = library.IsContentAbout(
            new OpenAiEntity(
                mockOpenAiApi.Object, 
                ModelName, 
                0, 
                0, 
                0, 
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
        var library = new OpenAiLibrary();
        var result = library.IsContentAbout(
            new OpenAiEntity(
                mockOpenAiApi.Object, 
                ModelName, 
                0, 
                0, 
                0, 
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
        var library = new OpenAiLibrary();
        var result = library.TranslateContent(
            new OpenAiEntity(
                mockOpenAiApi.Object, 
                ModelName, 
                0, 
                0, 
                0, 
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
        var library = new OpenAiLibrary();
        var result = library.Entities(
            new OpenAiEntity(
                mockOpenAiApi.Object, 
                ModelName, 
                0, 
                0, 
                0, 
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
        var library = new OpenAiLibrary();
        var result = library.Entities(
            new OpenAiEntity(
                mockOpenAiApi.Object, 
                ModelName, 
                0, 
                0, 
                0, 
                0,
                CancellationToken.None),
            "content");
        
        Assert.AreEqual(0, result.Length);
    }

    private static Mock<IOpenAiApi> PrepareOpenAiApi(string systemResponse)
    {
        var mock = new Mock<IOpenAiApi>();
        mock.Setup(f => f.GetCompletionAsync(It.IsAny<OpenAiEntity>(), It.IsAny<IList<ChatMessage>>()))
            .Returns<OpenAiEntity, IList<ChatMessage>>((entity, messages) =>
                Task.FromResult(new CompletionResponse(systemResponse)));
        return mock;
    }
}
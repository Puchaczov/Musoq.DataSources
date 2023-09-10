using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.OpenAIHelpers;
using OpenAI_API.Models;

namespace Musoq.DataSources.OpenAI.Tests;

[TestClass]
public class OpenAiApiPlayground
{
    [Ignore]
    [TestMethod]
    public void DoSomeRealTests()
    {
        var library = new OpenAiLibrary();
        var entity = new OpenAiEntity(
            new OpenAiApi(
                Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new ApplicationException("Api key must be set")), 
            Model.ChatGPTTurbo, 
            0, 
            20, 
            0, 
            0);
        
        var isAbout = library.IsContentAbout(entity, "happy tuesday", "is about happiness");
    }
}
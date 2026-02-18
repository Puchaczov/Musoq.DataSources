using System;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
                Environment.GetEnvironmentVariable("OPENAI_API_KEY") ??
                throw new ApplicationException("Api key must be set")),
            Defaults.DefaultModel,
            0,
            20,
            0,
            0,
            CancellationToken.None);

        var file = new FileInfo(@"D:\Photos\Piotruś\iphone\test\AARD3200.JPG");
        var base64 = library.ToBase64(File.ReadAllBytes(file.FullName));

        var description = library.AskImage(entity, "describe the photo", base64);
        var isAbout = library.IsContentAbout(entity, "happy tuesday", "is about happiness");
    }
}
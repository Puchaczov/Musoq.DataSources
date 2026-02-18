using System.IO;
using System.Net.Http;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Musoq.DataSources.Ollama.Tests;

[TestClass]
public class OllamaApiPlayground
{
    [Ignore]
    [TestMethod]
    public void DoSomeRealTests()
    {
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();

        mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient());

        var library = new OllamaLibrary();
        var entity = new OllamaEntity(
            new OllamaApi(mockHttpClientFactory.Object),
            "llama3.2-vision:latest",
            0,
            CancellationToken.None);


        library.AskImage(
            entity,
            "How would you name this photo(filename)?",
            library.ToBase64(File.ReadAllBytes("D:\\Photos\\Piotruś\\iphone\\202403__\\FIIG0678.JPG")));
    }

    [Ignore]
    [TestMethod]
    public void DoSomeOtherRealTests()
    {
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();

        mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient());

        var library = new OllamaLibrary();
        var entity = new OllamaEntity(
            new OllamaApi(mockHttpClientFactory.Object),
            "llama3.2-vision:latest",
            0,
            CancellationToken.None);

        var response = library.AskImage(
            entity,
            "Does the photo contain a cat? reply with \"yes\" or \"no\"",
            library.ToBase64(
                File.ReadAllBytes("C:\\Users\\pucha\\OneDrive\\Pictures\\fotokozia\\jak-ze-starej-fotografii.jpg")));
    }
}
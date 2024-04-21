using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.Ollama.Tests;

[TestClass]
public class OllamaApiPlayground
{
    [Ignore]
    [TestMethod]
    public void DoSomeRealTests()
    {
        var library = new OllamaLibrary();
        var entity = new OllamaEntity(
            new OllamaApi(), 
            "llava:13b",
            0,
            CancellationToken.None);
        
        //var isAbout = library.IsContentAbout(entity, "happy tuesday", "is about happiness");
        //var translatedText = library.TranslateContent(entity, "happy tuesday", "english", "polish");
        library.AskImage(
            entity,
            "How would you name this photo(filename)?",
            library.ToBase64(File.ReadAllBytes("D:\\Photos\\Piotruś\\iphone\\202403__\\FIIG0678.JPG")));
    }
}
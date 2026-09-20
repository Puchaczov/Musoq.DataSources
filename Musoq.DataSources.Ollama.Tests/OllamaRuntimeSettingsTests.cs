using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Ollama.Tests;

[TestClass]
public class OllamaRuntimeSettingsTests
{
    [TestMethod]
    public void DescribeSourceRuntimeSettings_ShouldDeclareOllamaBaseUrl()
    {
        var requirements = new OllamaSchema().DescribeSourceRuntimeSettings("llm", null!);

        Assert.AreEqual(1, requirements.Count);
        AssertRequirement(
            requirements[0],
            "OLLAMA_BASE_URL",
            required: false,
            secret: false,
            SourceRuntimeSettingPhase.Execution);
    }

    private static void AssertRequirement(
        SourceRuntimeSettingRequirement requirement,
        string name,
        bool required,
        bool secret,
        SourceRuntimeSettingPhase phase)
    {
        Assert.AreEqual(name, requirement.Name);
        Assert.AreEqual(required, requirement.Required);
        Assert.AreEqual(secret, requirement.Secret);
        Assert.AreEqual(phase, requirement.Phases);
    }
}

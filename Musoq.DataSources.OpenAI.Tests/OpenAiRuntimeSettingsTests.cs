using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.OpenAI.Tests;

[TestClass]
public class OpenAiRuntimeSettingsTests
{
    [TestMethod]
    public void DescribeSourceRuntimeSettings_ShouldDeclareOpenAiApiKey()
    {
        var requirements = new OpenAiSchema().DescribeSourceRuntimeSettings("gpt", null!);

        Assert.AreEqual(1, requirements.Count);
        AssertRequirement(
            requirements[0],
            "OPENAI_API_KEY",
            required: true,
            secret: true,
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

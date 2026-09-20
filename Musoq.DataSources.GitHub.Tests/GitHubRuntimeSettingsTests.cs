using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.GitHub.Tests;

[TestClass]
public class GitHubRuntimeSettingsTests
{
    [TestMethod]
    public void DescribeSourceRuntimeSettings_ShouldDeclareGitHubToken()
    {
        var requirements = new GitHubSchema().DescribeSourceRuntimeSettings("repositories", null!);

        Assert.AreEqual(1, requirements.Count);
        AssertRequirement(
            requirements[0],
            "GITHUB_TOKEN",
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

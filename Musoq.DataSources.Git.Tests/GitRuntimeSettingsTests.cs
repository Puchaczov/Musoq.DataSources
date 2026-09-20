using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public class GitRuntimeSettingsTests
{
    [TestMethod]
    public void DescribeSourceRuntimeSettings_DeclaresOptionalHistoryBackendSettings()
    {
        var requirements = new GitSchema().DescribeSourceRuntimeSettings("filehistory", null!);

        Assert.AreEqual(3, requirements.Count);
        AssertRequirement(requirements[0], "GIT_HISTORY_BACKEND");
        AssertRequirement(requirements[1], "GIT_EXECUTABLE");
        AssertRequirement(requirements[2], "GIT_REFERENCE_BACKEND");
    }

    private static void AssertRequirement(SourceRuntimeSettingRequirement requirement, string name)
    {
        Assert.AreEqual(name, requirement.Name);
        Assert.IsFalse(requirement.Required);
        Assert.IsFalse(requirement.Secret);
        Assert.AreEqual(SourceRuntimeSettingPhase.Execution, requirement.Phases);
    }
}

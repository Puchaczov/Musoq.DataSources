using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Jira.Tests;

[TestClass]
public class JiraRuntimeSettingsTests
{
    [TestMethod]
    public void DescribeSourceRuntimeSettings_ShouldDeclareJiraCredentials()
    {
        var requirements = new JiraSchema().DescribeSourceRuntimeSettings("projects", null!);

        Assert.AreEqual(3, requirements.Count);
        AssertRequirement(
            requirements.Single(requirement => requirement.Name == "JIRA_URL"),
            required: true,
            secret: false);
        AssertRequirement(
            requirements.Single(requirement => requirement.Name == "JIRA_USERNAME"),
            required: true,
            secret: false);
        AssertRequirement(
            requirements.Single(requirement => requirement.Name == "JIRA_API_TOKEN"),
            required: true,
            secret: true);
    }

    private static void AssertRequirement(
        SourceRuntimeSettingRequirement requirement,
        bool required,
        bool secret)
    {
        Assert.AreEqual(required, requirement.Required);
        Assert.AreEqual(secret, requirement.Secret);
        Assert.AreEqual(SourceRuntimeSettingPhase.Execution, requirement.Phases);
    }
}

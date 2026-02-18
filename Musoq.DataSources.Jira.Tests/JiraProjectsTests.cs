using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Jira.Entities;
using Musoq.DataSources.Jira.Tests.TestHelpers;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.Jira.Tests;

[TestClass]
public class JiraProjectsTests
{
    static JiraProjectsTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void WhenProjectsQueried_ShouldReturnValues()
    {
        var api = new Mock<IJiraApi>();

        api.Setup(f => f.GetProjectsAsync())
            .ReturnsAsync(new List<IJiraProject>
            {
                MockEntityFactory.CreateProject(key: "PROJ1", name: "Project One"),
                MockEntityFactory.CreateProject(key: "PROJ2", name: "Project Two")
            });

        var query = "select Key, Name from #jira.projects()";

        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);

        var table = vm.Run();

        Assert.AreEqual(2, table.Count);
        Assert.IsTrue(table.Any(row => (string)row[0] == "PROJ1" && (string)row[1] == "Project One"));
        Assert.IsTrue(table.Any(row => (string)row[0] == "PROJ2" && (string)row[1] == "Project Two"));
    }

    [TestMethod]
    public void WhenProjectsQueried_ShouldReturnAllColumns()
    {
        var api = new Mock<IJiraApi>();

        api.Setup(f => f.GetProjectsAsync())
            .ReturnsAsync(new List<IJiraProject>
            {
                MockEntityFactory.CreateProject(
                    "10000",
                    "TEST",
                    "Test Project",
                    "A test project description",
                    "projectlead",
                    "Development"
                )
            });

        var query = "select Id, Key, Name, Description, Lead, Category from #jira.projects()";

        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        var table = vm.Run();

        Assert.AreEqual(1, table.Count);
        var row = table[0];
        Assert.AreEqual("10000", row[0]);
        Assert.AreEqual("TEST", row[1]);
        Assert.AreEqual("Test Project", row[2]);
        Assert.AreEqual("A test project description", row[3]);
        Assert.AreEqual("projectlead", row[4]);
        Assert.AreEqual("Development", row[5]);
    }

    [TestMethod]
    public void WhenProjectsFilteredByKey_ShouldReturnFilteredResults()
    {
        var api = new Mock<IJiraApi>();

        api.Setup(f => f.GetProjectsAsync())
            .ReturnsAsync(new List<IJiraProject>
            {
                MockEntityFactory.CreateProject(key: "PROJ1", name: "Project One"),
                MockEntityFactory.CreateProject(key: "PROJ2", name: "Project Two"),
                MockEntityFactory.CreateProject(key: "OTHER", name: "Other Project")
            });

        var query = "select Key, Name from #jira.projects() where Key = 'PROJ1'";

        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        var table = vm.Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("PROJ1", table[0][0]);
    }

    [TestMethod]
    public void WhenNoProjectsFound_ShouldReturnEmptyTable()
    {
        var api = new Mock<IJiraApi>();

        api.Setup(f => f.GetProjectsAsync())
            .ReturnsAsync(new List<IJiraProject>());

        var query = "select Key, Name from #jira.projects()";

        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        var table = vm.Run();

        Assert.AreEqual(0, table.Count);
    }

    private static CompiledQuery CreateAndRunVirtualMachineWithResponse(string script, IJiraApi api)
    {
        var mockSchemaProvider = new Mock<ISchemaProvider>();

        mockSchemaProvider.Setup(f => f.GetSchema(It.IsAny<string>())).Returns(
            new JiraSchema(api));

        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            mockSchemaProvider.Object,
            new Dictionary<uint, IReadOnlyDictionary<string, string>>
            {
                {
                    0, new Dictionary<string, string>
                    {
                        { "JIRA_URL", "https://test.atlassian.net" },
                        { "JIRA_USERNAME", "test@example.com" },
                        { "JIRA_API_TOKEN", "test_token" }
                    }
                }
            });
    }
}
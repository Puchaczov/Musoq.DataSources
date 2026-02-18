using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Jira.Entities;
using Musoq.DataSources.Jira.Tests.TestHelpers;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.Jira.Tests;

[TestClass]
public class JiraIssuesTests
{
    static JiraIssuesTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void WhenIssuesQueried_ShouldReturnValues()
    {
        var api = new Mock<IJiraApi>();

        api.Setup(f => f.GetIssuesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<IJiraIssue>
            {
                MockEntityFactory.CreateIssue("TEST-1", summary: "First issue", status: "Open"),
                MockEntityFactory.CreateIssue("TEST-2", summary: "Second issue", status: "In Progress")
            });

        var query = "select Key, Summary, Status from #jira.issues('TEST')";

        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);

        var table = vm.Run();

        Assert.AreEqual(2, table.Count);
        Assert.IsTrue(table.Any(row => (string)row[0] == "TEST-1"));
        Assert.IsTrue(table.Any(row => (string)row[0] == "TEST-2"));
    }

    [TestMethod]
    public void WhenIssuesFilteredByStatus_ShouldReturnFilteredResults()
    {
        var api = new Mock<IJiraApi>();

        api.Setup(f => f.GetIssuesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<IJiraIssue>
            {
                MockEntityFactory.CreateIssue("TEST-1", summary: "Open issue", status: "Open"),
                MockEntityFactory.CreateIssue("TEST-2", summary: "Closed issue", status: "Closed")
            });

        var query = "select Key, Summary, Status from #jira.issues('TEST') where Status = 'Open'";

        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        var table = vm.Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("TEST-1", table[0][0]);
        Assert.AreEqual("Open", table[0][2]);
    }

    [TestMethod]
    public void WhenIssuesFilteredByAssignee_ShouldReturnFilteredResults()
    {
        var api = new Mock<IJiraApi>();

        api.Setup(f => f.GetIssuesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<IJiraIssue>
            {
                MockEntityFactory.CreateIssue("TEST-1", assignee: "john.doe"),
                MockEntityFactory.CreateIssue("TEST-2", assignee: "jane.doe")
            });

        var query = "select Key, Assignee from #jira.issues('TEST') where Assignee = 'john.doe'";

        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        var table = vm.Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("john.doe", table[0][1]);
    }

    [TestMethod]
    public void WhenIssuesFilteredByType_ShouldReturnFilteredResults()
    {
        var api = new Mock<IJiraApi>();

        api.Setup(f => f.GetIssuesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<IJiraIssue>
            {
                MockEntityFactory.CreateIssue("TEST-1", type: "Bug"),
                MockEntityFactory.CreateIssue("TEST-2", type: "Story")
            });

        var query = "select Key, Type from #jira.issues('TEST') where Type = 'Bug'";

        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        var table = vm.Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("Bug", table[0][1]);
    }

    [TestMethod]
    public void WhenIssuesFilteredByMultipleConditions_ShouldReturnFilteredResults()
    {
        var api = new Mock<IJiraApi>();

        api.Setup(f => f.GetIssuesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<IJiraIssue>
            {
                MockEntityFactory.CreateIssue("TEST-1", status: "Open", priority: "High"),
                MockEntityFactory.CreateIssue("TEST-2", status: "Open", priority: "Low"),
                MockEntityFactory.CreateIssue("TEST-3", status: "Closed", priority: "High")
            });

        var query = "select Key from #jira.issues('TEST') where Status = 'Open' and Priority = 'High'";

        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        var table = vm.Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("TEST-1", table[0][0]);
    }

    [TestMethod]
    public void WhenIssuesQueried_ShouldReturnAllColumns()
    {
        var api = new Mock<IJiraApi>();
        var createdAt = DateTimeOffset.UtcNow.AddDays(-5);
        var updatedAt = DateTimeOffset.UtcNow;
        var dueDate = DateTime.Today.AddDays(7);

        api.Setup(f => f.GetIssuesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<IJiraIssue>
            {
                MockEntityFactory.CreateIssue(
                    "TEST-123",
                    "10001",
                    "Test summary",
                    "Test description",
                    "Story",
                    "In Progress",
                    "High",
                    assignee: "developer",
                    reporter: "product.owner",
                    projectKey: "TEST",
                    createdAt: createdAt,
                    updatedAt: updatedAt,
                    dueDate: dueDate,
                    labels: "label1, label2",
                    components: "Backend, API"
                )
            });

        var query = @"
            select 
                Key, 
                Id, 
                Summary, 
                Description, 
                Type, 
                Status, 
                Priority, 
                Assignee, 
                Reporter, 
                ProjectKey,
                Labels,
                Components
            from #jira.issues('TEST')";

        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        var table = vm.Run();

        Assert.AreEqual(1, table.Count);
        var row = table[0];
        Assert.AreEqual("TEST-123", row[0]);
        Assert.AreEqual("10001", row[1]);
        Assert.AreEqual("Test summary", row[2]);
        Assert.AreEqual("Test description", row[3]);
        Assert.AreEqual("Story", row[4]);
        Assert.AreEqual("In Progress", row[5]);
        Assert.AreEqual("High", row[6]);
        Assert.AreEqual("developer", row[7]);
        Assert.AreEqual("product.owner", row[8]);
        Assert.AreEqual("TEST", row[9]);
        Assert.AreEqual("label1, label2", row[10]);
        Assert.AreEqual("Backend, API", row[11]);
    }

    [TestMethod]
    public void WhenNoIssuesFound_ShouldReturnEmptyTable()
    {
        var api = new Mock<IJiraApi>();

        api.Setup(f => f.GetIssuesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<IJiraIssue>());

        var query = "select Key, Summary from #jira.issues('EMPTY')";

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
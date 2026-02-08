using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Jira.Entities;
using Musoq.DataSources.Jira.Tests.TestHelpers;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;

namespace Musoq.DataSources.Jira.Tests;

[TestClass]
public class JiraCommentsTests
{
    [TestMethod]
    public void WhenCommentsQueried_ShouldReturnValues()
    {
        var api = new Mock<IJiraApi>();

        api.Setup(f => f.GetCommentsAsync("TEST-123"))
            .ReturnsAsync(new List<IJiraComment>
            {
                MockEntityFactory.CreateComment(id: "1", issueKey: "TEST-123", body: "First comment", author: "user1"),
                MockEntityFactory.CreateComment(id: "2", issueKey: "TEST-123", body: "Second comment", author: "user2")
            });

        var query = "select Id, IssueKey, Body, Author from #jira.comments('TEST-123')";

        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);

        var table = vm.Run();

        Assert.AreEqual(2, table.Count);
        Assert.IsTrue(table.Any(row => (string)row[0] == "1" && (string)row[2] == "First comment"));
        Assert.IsTrue(table.Any(row => (string)row[0] == "2" && (string)row[2] == "Second comment"));
    }

    [TestMethod]
    public void WhenNoCommentsFound_ShouldReturnEmptyTable()
    {
        var api = new Mock<IJiraApi>();

        api.Setup(f => f.GetCommentsAsync("TEST-999"))
            .ReturnsAsync(new List<IJiraComment>());

        var query = "select Id, Body from #jira.comments('TEST-999')";

        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        var table = vm.Run();

        Assert.AreEqual(0, table.Count);
    }

    [TestMethod]
    public void WhenCommentsFilteredByAuthor_ShouldReturnFilteredResults()
    {
        var api = new Mock<IJiraApi>();

        api.Setup(f => f.GetCommentsAsync("TEST-123"))
            .ReturnsAsync(new List<IJiraComment>
            {
                MockEntityFactory.CreateComment(id: "1", body: "Comment by user1", author: "user1"),
                MockEntityFactory.CreateComment(id: "2", body: "Comment by user2", author: "user2"),
                MockEntityFactory.CreateComment(id: "3", body: "Another by user1", author: "user1")
            });

        var query = "select Id, Body, Author from #jira.comments('TEST-123') where Author = 'user1'";

        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        var table = vm.Run();

        Assert.AreEqual(2, table.Count);
        Assert.IsTrue(table.All(row => (string)row[2] == "user1"));
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
                        {"JIRA_URL", "https://test.atlassian.net"},
                        {"JIRA_USERNAME", "test@example.com"},
                        {"JIRA_API_TOKEN", "test_token"}
                    }
                }
            });
    }

    static JiraCommentsTests()
    {
        Culture.ApplyWithDefaultCulture();
    }
}

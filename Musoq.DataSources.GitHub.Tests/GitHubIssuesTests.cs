using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.GitHub.Entities;
using Musoq.DataSources.GitHub.Tests.TestHelpers;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;
using Octokit;

namespace Musoq.DataSources.GitHub.Tests;

[TestClass]
public class GitHubIssuesTests
{
    [TestMethod]
    public void WhenIssuesQueried_ShouldReturnValues()
    {
        var api = new Mock<IGitHubApi>();
        
        api.Setup(f => f.GetIssuesAsync("testowner", "testrepo", It.IsAny<RepositoryIssueRequest>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<IssueEntity>
            {
                MockEntityFactory.CreateIssue(id: 1, number: 101, title: "Bug: Something is broken", state: "open"),
                MockEntityFactory.CreateIssue(id: 2, number: 102, title: "Feature: Add new feature", state: "open")
            });
        
        var query = "select Id, Number, Title, State from #github.issues('testowner', 'testrepo')";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
        
        // Order is not guaranteed, so check that both issues exist
        Assert.IsTrue(table.Any(row => (long)row[0] == 1L && (int)row[1] == 101));
        Assert.IsTrue(table.Any(row => (long)row[0] == 2L && (int)row[1] == 102));
        
        var issue1 = table.First(row => (long)row[0] == 1L);
        Assert.AreEqual(101, issue1[1]);
        Assert.AreEqual("Bug: Something is broken", issue1[2]);
        Assert.AreEqual("open", issue1[3]);
        
        api.Verify(f => f.GetIssuesAsync("testowner", "testrepo", It.IsAny<RepositoryIssueRequest>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
    }
    
    [TestMethod]
    public void WhenIssuesFilteredByState_ShouldReturnFilteredResults()
    {
        var api = new Mock<IGitHubApi>();
        
        api.Setup(f => f.GetIssuesAsync("testowner", "testrepo", It.IsAny<RepositoryIssueRequest>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<IssueEntity>
            {
                MockEntityFactory.CreateIssue(id: 1, number: 101, title: "Open issue", state: "open"),
                MockEntityFactory.CreateIssue(id: 2, number: 102, title: "Closed issue", state: "closed")
            });
        
        var query = "select Number, Title, State from #github.issues('testowner', 'testrepo') where State = 'open'";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(101, table[0][0]);
        Assert.AreEqual("Open issue", table[0][1]);
    }
    
    [TestMethod]
    public void WhenIssuesFilteredByAuthor_ShouldReturnFilteredResults()
    {
        var api = new Mock<IGitHubApi>();
        
        api.Setup(f => f.GetIssuesAsync("testowner", "testrepo", It.IsAny<RepositoryIssueRequest>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<IssueEntity>
            {
                MockEntityFactory.CreateIssue(id: 1, number: 101, title: "Issue by user1", state: "open", authorLogin: "user1"),
                MockEntityFactory.CreateIssue(id: 2, number: 102, title: "Issue by user2", state: "open", authorLogin: "user2")
            });
        
        var query = "select Number, Title, AuthorLogin from #github.issues('testowner', 'testrepo') where AuthorLogin = 'user1'";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("user1", table[0][2]);
    }

    private static CompiledQuery CreateAndRunVirtualMachineWithResponse(string script, IGitHubApi api)
    {
        var mockSchemaProvider = new Mock<ISchemaProvider>();

        mockSchemaProvider.Setup(f => f.GetSchema(It.IsAny<string>())).Returns(
            new GitHubSchema(api));
        
        return InstanceCreatorHelpers.CompileForExecution(
            script, 
            Guid.NewGuid().ToString(), 
            mockSchemaProvider.Object, 
            new Dictionary<uint, IReadOnlyDictionary<string, string>>()
            {
                {0, new Dictionary<string, string>
                {
                    {"GITHUB_TOKEN", "test_token"}
                }}
            });
    }

    static GitHubIssuesTests()
    {
        Culture.ApplyWithDefaultCulture();
    }
}

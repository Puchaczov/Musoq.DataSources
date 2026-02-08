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
public class GitHubPullRequestsTests
{
    [TestMethod]
    public void WhenPullRequestsQueried_ShouldReturnValues()
    {
        var api = new Mock<IGitHubApi>();
        
        api.Setup(f => f.GetPullRequestsAsync("testowner", "testrepo", It.IsAny<PullRequestRequest>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<PullRequestEntity>
            {
                MockEntityFactory.CreatePullRequest(id: 1, number: 201, title: "Add new feature", state: "open"),
                MockEntityFactory.CreatePullRequest(id: 2, number: 202, title: "Fix bug", state: "closed", merged: true)
            });
        
        var query = "select Id, Number, Title, State, Merged from #github.pullrequests('testowner', 'testrepo')";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
        
        // Order is not guaranteed, so check that both PRs exist
        Assert.IsTrue(table.Any(row => (long)row[0] == 1L && (int)row[1] == 201));
        Assert.IsTrue(table.Any(row => (long)row[0] == 2L && (int)row[1] == 202));
        
        var pr1 = table.First(row => (long)row[0] == 1L);
        Assert.AreEqual(201, pr1[1]);
        Assert.AreEqual("Add new feature", pr1[2]);
        Assert.AreEqual("open", pr1[3]);
        Assert.AreEqual(false, pr1[4]);
        
        var pr2 = table.First(row => (long)row[0] == 2L);
        Assert.AreEqual(202, pr2[1]);
        Assert.AreEqual(true, pr2[4]);
        
        api.Verify(f => f.GetPullRequestsAsync("testowner", "testrepo", It.IsAny<PullRequestRequest>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
    }
    
    [TestMethod]
    public void WhenPullRequestsFilteredByState_ShouldReturnFilteredResults()
    {
        var api = new Mock<IGitHubApi>();
        
        api.Setup(f => f.GetPullRequestsAsync("testowner", "testrepo", It.IsAny<PullRequestRequest>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<PullRequestEntity>
            {
                MockEntityFactory.CreatePullRequest(id: 1, number: 201, title: "Open PR", state: "open"),
                MockEntityFactory.CreatePullRequest(id: 2, number: 202, title: "Closed PR", state: "closed")
            });
        
        var query = "select Number, Title from #github.pullrequests('testowner', 'testrepo') where State = 'open'";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(201, table[0][0]);
    }
    
    [TestMethod]
    public void WhenPullRequestsFilteredByMerged_ShouldReturnMergedOnly()
    {
        var api = new Mock<IGitHubApi>();
        
        api.Setup(f => f.GetPullRequestsAsync("testowner", "testrepo", It.IsAny<PullRequestRequest>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<PullRequestEntity>
            {
                MockEntityFactory.CreatePullRequest(id: 1, number: 201, title: "Merged PR", state: "closed", merged: true),
                MockEntityFactory.CreatePullRequest(id: 2, number: 202, title: "Closed but not merged", state: "closed", merged: false)
            });
        
        var query = "select Number, Title, Merged from #github.pullrequests('testowner', 'testrepo') where Merged = true";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(201, table[0][0]);
        Assert.AreEqual(true, table[0][2]);
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

    static GitHubPullRequestsTests()
    {
        Culture.ApplyWithDefaultCulture();
    }
}

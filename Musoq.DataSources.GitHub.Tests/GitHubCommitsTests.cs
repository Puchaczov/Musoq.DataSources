using System;
using System.Collections.Generic;
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
public class GitHubCommitsTests
{
    [TestMethod]
    public void WhenCommitsQueried_ShouldReturnValues()
    {
        var api = new Mock<IGitHubApi>();
        
        api.Setup(f => f.GetCommitsAsync("testowner", "testrepo", It.IsAny<CommitRequest>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<CommitEntity>
            {
                MockEntityFactory.CreateCommit(sha: "abc123def456", message: "Initial commit", authorName: "Test User", authorEmail: "test@test.com"),
                MockEntityFactory.CreateCommit(sha: "def456abc789", message: "Add feature", authorName: "Test User", authorEmail: "test@test.com")
            });
        
        var query = "select Sha, ShortSha, Message, AuthorName, AuthorEmail from #github.commits('testowner', 'testrepo') order by Sha";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
        Assert.AreEqual("abc123def456", table[0][0]);
        Assert.AreEqual("abc123d", table[0][1]);
        Assert.AreEqual("Initial commit", table[0][2]);
        Assert.AreEqual("Test User", table[0][3]);
        Assert.AreEqual("test@test.com", table[0][4]);
        
        api.Verify(f => f.GetCommitsAsync("testowner", "testrepo", It.IsAny<CommitRequest>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
    }
    
    [TestMethod]
    public void WhenCommitsForBranchQueried_ShouldPassShaParameter()
    {
        var api = new Mock<IGitHubApi>();
        
        api.Setup(f => f.GetCommitsAsync("testowner", "testrepo", It.IsAny<CommitRequest>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<CommitEntity>
            {
                MockEntityFactory.CreateCommit(sha: "abc123def456", message: "Commit on branch", authorName: "Test User", authorEmail: "test@test.com")
            });
        
        var query = "select Sha, Message from #github.commits('testowner', 'testrepo', 'feature-branch')";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("abc123def456", table[0][0]);
        
        // Verify the branch name was passed as Sha to the GitHub API commit request
        api.Verify(f => f.GetCommitsAsync(
            "testowner", "testrepo",
            It.Is<CommitRequest>(r => r.Sha == "feature-branch"),
            It.IsAny<int?>(), It.IsAny<int?>()),
            Times.Once);
    }

    [TestMethod]
    public void WhenCommitsForMainBranchQueried_ShouldPassBranchNameToApi()
    {
        var api = new Mock<IGitHubApi>();
        
        api.Setup(f => f.GetCommitsAsync("testowner", "testrepo", It.IsAny<CommitRequest>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<CommitEntity>
            {
                MockEntityFactory.CreateCommit(sha: "abc123def456", message: "Commit on main", authorName: "Test User", authorEmail: "test@test.com"),
                MockEntityFactory.CreateCommit(sha: "def456abc789", message: "Another on main", authorName: "Test User", authorEmail: "test@test.com")
            });
        
        var query = "select c.Sha, c.Message from #github.commits('testowner', 'testrepo', 'main') c";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
        
        // Verify the branch name 'main' was passed as Sha to the GitHub API commit request
        api.Verify(f => f.GetCommitsAsync(
            "testowner", "testrepo",
            It.Is<CommitRequest>(r => r.Sha == "main"),
            It.IsAny<int?>(), It.IsAny<int?>()),
            Times.Once);
    }

    [TestMethod]
    public void WhenBranchSpecificCommitsQueried_ShouldReturnOnlyCommitsUniqueToHeadBranch()
    {
        var api = new Mock<IGitHubApi>();
        
        // Only the 2 commits unique to feature/my-feature (not on main)
        api.Setup(f => f.GetBranchSpecificCommitsAsync("testowner", "testrepo", "main", "feature/my-feature"))
            .ReturnsAsync(new List<CommitEntity>
            {
                MockEntityFactory.CreateCommit(sha: "feat001def456", message: "Add feature part 1", authorName: "Dev User", authorEmail: "dev@test.com"),
                MockEntityFactory.CreateCommit(sha: "feat002abc789", message: "Add feature part 2", authorName: "Dev User", authorEmail: "dev@test.com")
            });
        
        var query = "select c.Sha, c.Message, c.AuthorName from #github.branchcommits('testowner', 'testrepo', 'main', 'feature/my-feature') c order by c.Sha";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
        Assert.AreEqual("feat001def456", (string)table[0][0]);
        Assert.AreEqual("Add feature part 1", (string)table[0][1]);
        Assert.AreEqual("feat002abc789", (string)table[1][0]);
        Assert.AreEqual("Add feature part 2", (string)table[1][1]);
        
        api.Verify(f => f.GetBranchSpecificCommitsAsync("testowner", "testrepo", "main", "feature/my-feature"), Times.Once);
        // Regular GetCommitsAsync should NOT be called
        api.Verify(f => f.GetCommitsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CommitRequest>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
    }

    [TestMethod]
    public void WhenBranchSpecificCommitsQueried_WithNoUniqueCommits_ShouldReturnEmpty()
    {
        var api = new Mock<IGitHubApi>();
        
        api.Setup(f => f.GetBranchSpecificCommitsAsync("testowner", "testrepo", "main", "feature/empty"))
            .ReturnsAsync(new List<CommitEntity>());
        
        var query = "select c.Sha from #github.branchcommits('testowner', 'testrepo', 'main', 'feature/empty') c";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(0, table.Count);
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

    static GitHubCommitsTests()
    {
        Culture.ApplyWithDefaultCulture();
    }
}

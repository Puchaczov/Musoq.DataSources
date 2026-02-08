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
public class GitHubBranchesTests
{
    [TestMethod]
    public void WhenBranchesQueried_ShouldReturnValues()
    {
        var api = new Mock<IGitHubApi>();
        
        api.Setup(f => f.GetBranchesAsync("testowner", "testrepo", It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<BranchEntity>
            {
                MockEntityFactory.CreateBranch(name: "main", sha: "abc123", isProtected: true, owner: "testowner", repo: "testrepo"),
                MockEntityFactory.CreateBranch(name: "develop", sha: "def456", isProtected: false, owner: "testowner", repo: "testrepo"),
                MockEntityFactory.CreateBranch(name: "feature-branch", sha: "ghi789", isProtected: false, owner: "testowner", repo: "testrepo")
            });
        
        var query = "select Name, CommitSha, Protected, RepositoryOwner, RepositoryName from #github.branches('testowner', 'testrepo')";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(3, table.Count);
        
        // Check first row - note: order may not be guaranteed, so we check existence
        var names = new List<string> { (string)table[0][0], (string)table[1][0], (string)table[2][0] };
        Assert.IsTrue(names.Contains("main"), "Expected 'main' branch");
        Assert.IsTrue(names.Contains("develop"), "Expected 'develop' branch");
        Assert.IsTrue(names.Contains("feature-branch"), "Expected 'feature-branch' branch");
        
        // Check the main branch details (find it first)
        var mainRow = table.FirstOrDefault(r => (string)r[0] == "main");
        Assert.IsNotNull(mainRow, "Main branch should exist");
        Assert.AreEqual("abc123", mainRow?[1]);
        Assert.AreEqual(true, mainRow?[2]);
        Assert.AreEqual("testowner", mainRow?[3]);
        Assert.AreEqual("testrepo", mainRow?[4]);
        
        api.Verify(f => f.GetBranchesAsync("testowner", "testrepo", It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
    }
    
    [TestMethod]
    public void WhenBranchesFilteredByProtected_ShouldReturnProtectedOnly()
    {
        var api = new Mock<IGitHubApi>();
        
        api.Setup(f => f.GetBranchesAsync("testowner", "testrepo", It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<BranchEntity>
            {
                MockEntityFactory.CreateBranch(name: "main", sha: "abc123", isProtected: true, owner: "testowner", repo: "testrepo"),
                MockEntityFactory.CreateBranch(name: "develop", sha: "def456", isProtected: true, owner: "testowner", repo: "testrepo"),
                MockEntityFactory.CreateBranch(name: "feature", sha: "ghi789", isProtected: false, owner: "testowner", repo: "testrepo")
            });
        
        var query = "select Name from #github.branches('testowner', 'testrepo') where Protected = true";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
        // Order is not guaranteed, check both protected branches exist
        Assert.IsTrue(table.Any(row => (string)row[0] == "main"));
        Assert.IsTrue(table.Any(row => (string)row[0] == "develop"));
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

    static GitHubBranchesTests()
    {
        Culture.ApplyWithDefaultCulture();
    }
}

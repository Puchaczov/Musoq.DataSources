using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
public class GitHubRepositoriesTests
{
    [TestMethod]
    public void WhenRepositoriesQueried_ShouldReturnValues()
    {
        var api = new Mock<IGitHubApi>();
        
        api.Setup(f => f.GetUserRepositoriesAsync(It.IsAny<RepositoryRequest>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<RepositoryEntity>
            {
                MockEntityFactory.CreateRepository(id: 1, name: "test-repo", fullName: "testowner/test-repo", description: "Test repository")
            });
        
        var query = "select Id, Name, FullName, Description, StargazersCount, ForksCount, Language from #github.repositories()";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(1L, table[0][0]);
        Assert.AreEqual("test-repo", table[0][1]);
        Assert.AreEqual("testowner/test-repo", table[0][2]);
        Assert.AreEqual("Test repository", table[0][3]);
        
        api.Verify(f => f.GetUserRepositoriesAsync(It.IsAny<RepositoryRequest>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
    }
    
    [TestMethod]
    public void WhenRepositoriesForOwnerQueried_ShouldReturnValues()
    {
        var api = new Mock<IGitHubApi>();
        
        api.Setup(f => f.GetRepositoriesForOwnerAsync("testowner", It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<RepositoryEntity>
            {
                MockEntityFactory.CreateRepository(id: 1, name: "repo1", fullName: "testowner/repo1", description: "First repo"),
                MockEntityFactory.CreateRepository(id: 2, name: "repo2", fullName: "testowner/repo2", description: "Second repo")
            });
        
        var query = "select Id, Name, FullName from #github.repositories('testowner') order by Id";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        Assert.AreEqual(2, table.Count);
        Assert.AreEqual(1L, table[0][0]);
        Assert.AreEqual("repo1", table[0][1]);
        Assert.AreEqual(2L, table[1][0]);
        Assert.AreEqual("repo2", table[1][1]);
        
        api.Verify(f => f.GetRepositoriesForOwnerAsync("testowner", It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
    }
    
    [TestMethod]
    public void WhenRepositoriesFilteredByLanguage_ShouldApplyFilter()
    {
        var api = new Mock<IGitHubApi>();
        
        api.Setup(f => f.GetUserRepositoriesAsync(It.IsAny<RepositoryRequest>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<RepositoryEntity>
            {
                MockEntityFactory.CreateRepository(id: 1, name: "csharp-repo", fullName: "owner/csharp-repo", description: "C# project", language: "C#"),
                MockEntityFactory.CreateRepository(id: 2, name: "python-repo", fullName: "owner/python-repo", description: "Python project", language: "Python")
            });
        
        var query = "select Name, Language from #github.repositories() where Language = 'C#'";
        
        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);
        
        var table = vm.Run();
        
        // Filter is applied in-query since this specific filter isn't pushed down
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("csharp-repo", table[0][0]);
        Assert.AreEqual("C#", table[0][1]);
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

    static GitHubRepositoriesTests()
    {
        Culture.ApplyWithDefaultCulture();
    }
}

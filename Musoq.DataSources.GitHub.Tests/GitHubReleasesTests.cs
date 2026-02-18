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

namespace Musoq.DataSources.GitHub.Tests;

[TestClass]
public class GitHubReleasesTests
{
    static GitHubReleasesTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void WhenReleasesQueried_ShouldReturnValues()
    {
        var api = new Mock<IGitHubApi>();

        api.Setup(f => f.GetReleasesAsync("testowner", "testrepo", It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<ReleaseEntity>
            {
                MockEntityFactory.CreateRelease(1, "v1.0.0", "Version 1.0.0", prerelease: false, draft: false),
                MockEntityFactory.CreateRelease(2, "v2.0.0-beta", "Version 2.0.0 Beta", prerelease: true, draft: false),
                MockEntityFactory.CreateRelease(3, "v2.0.0", "Version 2.0.0", prerelease: false, draft: false)
            });

        var query = "select Id, TagName, Name, Draft, Prerelease from #github.releases('testowner', 'testrepo')";

        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);

        var table = vm.Run();

        Assert.AreEqual(3, table.Count);


        var v1Release = table.FirstOrDefault(row => (string)row[1] == "v1.0.0");
        Assert.IsNotNull(v1Release);
        Assert.AreEqual(1L, v1Release[0]);
        Assert.AreEqual("Version 1.0.0", v1Release[2]);
        Assert.AreEqual(false, v1Release[3]);
        Assert.AreEqual(false, v1Release[4]);

        var v2BetaRelease = table.FirstOrDefault(row => (string)row[1] == "v2.0.0-beta");
        Assert.IsNotNull(v2BetaRelease);
        Assert.AreEqual(true, v2BetaRelease[4]);

        api.Verify(f => f.GetReleasesAsync("testowner", "testrepo", It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
    }

    [TestMethod]
    public void WhenReleasesFilteredByPrerelease_ShouldReturnNonPrereleaseOnly()
    {
        var api = new Mock<IGitHubApi>();

        api.Setup(f => f.GetReleasesAsync("testowner", "testrepo", It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<ReleaseEntity>
            {
                MockEntityFactory.CreateRelease(1, "v1.0.0", "Version 1.0.0", prerelease: false, draft: false),
                MockEntityFactory.CreateRelease(2, "v2.0.0-beta", "Version 2.0.0 Beta", prerelease: true, draft: false)
            });

        var query = "select TagName from #github.releases('testowner', 'testrepo') where Prerelease = false";

        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);

        var table = vm.Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("v1.0.0", table[0][0]);
    }

    [TestMethod]
    public void WhenReleasesFilteredByDraft_ShouldExcludeDrafts()
    {
        var api = new Mock<IGitHubApi>();

        api.Setup(f => f.GetReleasesAsync("testowner", "testrepo", It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<ReleaseEntity>
            {
                MockEntityFactory.CreateRelease(1, "v1.0.0", "Version 1.0.0", prerelease: false, draft: false),
                MockEntityFactory.CreateRelease(2, "v2.0.0-draft", "Draft Release", prerelease: false, draft: true)
            });

        var query = "select TagName from #github.releases('testowner', 'testrepo') where Draft = false";

        var vm = CreateAndRunVirtualMachineWithResponse(query, api.Object);

        var table = vm.Run();

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual("v1.0.0", table[0][0]);
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
            new Dictionary<uint, IReadOnlyDictionary<string, string>>
            {
                {
                    0, new Dictionary<string, string>
                    {
                        { "GITHUB_TOKEN", "test_token" }
                    }
                }
            });
    }
}
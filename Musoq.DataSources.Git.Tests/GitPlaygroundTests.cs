using Musoq.DataSources.Git.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Parser.Helpers;

namespace Musoq.DataSources.Git.Tests;

[Ignore]
[TestClass]
public class GitPlaygroundTests
{
    private const string RepositoryPath = @"D:\repos\Musoq.DataSources";

    static GitPlaygroundTests()
    {
        Culture.ApplyWithDefaultCulture();
    }

    [TestMethod]
    public void FileHistoryPlayground_ShouldBeIgnored()
    {
        var query = $"select * from #git.filehistory('{RepositoryPath.Escape()}', 'Musoq.DataSources.Git.csproj')";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void FileHistoryTakePlayground_ShouldBeIgnored()
    {
        var query = $"select * from #git.filehistory('{RepositoryPath.Escape()}', 'Musoq.DataSources.Git.csproj', 1)";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void FileHistorySkipTakePlayground_ShouldBeIgnored()
    {
        var query =
            $"select * from #git.filehistory('{RepositoryPath.Escape()}', 'Musoq.DataSources.Git.csproj', 1, 2)";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void FileHistoryPlaygroundDesc_ShouldBeIgnored()
    {
        var query = $"desc #git.filehistory('{RepositoryPath.Escape()}', 'Musoq.DataSources.Git.csproj')";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void FileHistoryWildcardPlayground_ShouldBeIgnored()
    {
        var query = $"select * from #git.filehistory('{RepositoryPath.Escape()}', '*.csproj')";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void RepositoryPlayground_ShouldBeIgnored()
    {
        var query = $"select * from #git.repository('{RepositoryPath.Escape()}')";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void CommitsPlayground_ShouldBeIgnored()
    {
        var query = $"select * from #git.commits('{RepositoryPath.Escape()}')";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void BranchesPlayground_ShouldBeIgnored()
    {
        var query = $"select * from #git.branches('{RepositoryPath.Escape()}')";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void TagsPlayground_ShouldBeIgnored()
    {
        var query = $"select * from #git.tags('{RepositoryPath.Escape()}')";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void StatusPlayground_ShouldBeIgnored()
    {
        var query = $"select * from #git.status('{RepositoryPath.Escape()}')";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    [TestMethod]
    public void RemotesPlayground_ShouldBeIgnored()
    {
        var query = $"select * from #git.remotes('{RepositoryPath.Escape()}')";

        var vm = CreateAndRunVirtualMachineWithResponse(query);

        var table = vm.Run();
    }

    private static CompiledQuery CreateAndRunVirtualMachineWithResponse(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            new GitSchemaProvider(),
            new Dictionary<uint, IReadOnlyDictionary<string, string>>
            {
                { 0, new Dictionary<string, string>() },
                { 1, new Dictionary<string, string>() }
            });
    }
}
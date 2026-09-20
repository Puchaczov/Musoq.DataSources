using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging.Abstractions;
using Musoq.DataSources.Roslyn.CliCommands;
using Musoq.DataSources.Roslyn.Components.NuGet;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public sealed class SolutionStatusTests
{
    private string _originalCacheDirectory = null!;
    private ResolveValueStrategy _originalStrategy;

    [TestInitialize]
    public void Initialize()
    {
        _originalCacheDirectory = SolutionOperationsCommand.DefaultCacheDirectoryPath;
        _originalStrategy = SolutionOperationsCommand.ResolveValueStrategy;
        SolutionOperationsCommand.Solutions.Clear();
        SolutionOperationsCommand.DefaultCacheDirectoryPath = @"C:\cache";
        SolutionOperationsCommand.ResolveValueStrategy = ResolveValueStrategy.UseNugetOrgApiOnly;
        LifecycleHooks.Logger = NullLogger.Instance;
    }

    [TestCleanup]
    public void Cleanup()
    {
        SolutionOperationsCommand.Solutions.Clear();
        SolutionOperationsCommand.DefaultCacheDirectoryPath = _originalCacheDirectory;
        SolutionOperationsCommand.ResolveValueStrategy = _originalStrategy;
        LifecycleHooks.Logger = null;
    }

    [TestMethod]
    public async Task StatusSnapshotOmitsSolutionsWhenEmpty()
    {
        var result = await LifecycleHooks.GetAsync(["solution", "status"], CancellationToken.None);

        Assert.AreEqual(0, result.ReturnValue);
        Assert.AreEqual(0, result.Exceptions.Length);
        Assert.AreEqual(
            string.Join(Environment.NewLine,
                "Loaded solutions: 0",
                "Cache directory: C:\\cache",
                "Resolve value strategy: UseNugetOrgApiOnly"),
            result.Value);
    }

    [TestMethod]
    public async Task StatusSnapshotSortsSolutionPathsOrdinally()
    {
        using var workspace = new AdhocWorkspace();
        SolutionOperationsCommand.Solutions.TryAdd(@"C:\repos\z.sln", workspace.CurrentSolution);
        SolutionOperationsCommand.Solutions.TryAdd(@"C:\repos\a.sln", workspace.CurrentSolution);

        var result = await LifecycleHooks.GetAsync(["solution", "status"], CancellationToken.None);

        Assert.AreEqual(0, result.ReturnValue);
        Assert.AreEqual(
            string.Join(Environment.NewLine,
                "Loaded solutions: 2",
                "Cache directory: C:\\cache",
                "Resolve value strategy: UseNugetOrgApiOnly",
                "Solutions:",
                "  C:\\repos\\a.sln",
                "  C:\\repos\\z.sln"),
            result.Value);
    }
}

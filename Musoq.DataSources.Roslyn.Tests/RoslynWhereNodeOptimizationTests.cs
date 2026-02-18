using System.Globalization;
using Moq;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.DataSources.Roslyn.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Parser.Helpers;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public class RoslynWhereNodeOptimizationTests
{
    static RoslynWhereNodeOptimizationTests()
    {
        Culture.Apply(CultureInfo.GetCultureInfo("en-EN"));
    }

    private static string Solution1SolutionPath =>
        Path.Combine(StartDirectory, "TestsSolutions", "Solution1", "Solution1.sln");

    private static string StartDirectory
    {
        get
        {
            var filePath = typeof(RoslynWhereNodeOptimizationTests).Assembly.Location;
            var directory = Path.GetDirectoryName(filePath);

            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Directory is empty.");

            return directory;
        }
    }

    [TestMethod]
    public void WhenProjectsFilteredByAssemblyName_ShouldReturnMatchingProject()
    {
        var query = $@"
            select p.AssemblyName, p.Language
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s
            cross apply s.Projects p
            where p.AssemblyName = 'Solution1.ClassLibrary1'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count,
            "Exactly one project with AssemblyName = 'Solution1.ClassLibrary1' should be returned");
        Assert.AreEqual("Solution1.ClassLibrary1", result[0][0]?.ToString());
        Assert.AreEqual("C#", result[0][1]?.ToString());
    }

    [TestMethod]
    public void WhenProjectsFilteredByLanguage_ShouldReturnAllCSharpProjects()
    {
        var query = $@"
            select p.AssemblyName, p.Language
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s
            cross apply s.Projects p
            where p.Language = 'C#'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(2, result.Count, "Both projects in Solution1 are C# and should be returned");
        Assert.IsTrue(result.All(r => r[1]?.ToString() == "C#"), "All returned projects should have Language = C#");
        Assert.IsTrue(result.Any(r => r[0]?.ToString() == "Solution1.ClassLibrary1"),
            "Solution1.ClassLibrary1 should be in results");
        Assert.IsTrue(result.Any(r => r[0]?.ToString() == "Solution1.ClassLibrary1.Tests"),
            "Solution1.ClassLibrary1.Tests should be in results");
    }

    [TestMethod]
    public void WhenProjectsFilteredByNonExistentAssemblyName_ShouldReturnNoProjects()
    {
        var query = $@"
            select p.AssemblyName
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s
            cross apply s.Projects p
            where p.AssemblyName = 'NonExistentProject'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(0, result.Count, "No projects should match a non-existent AssemblyName");
    }

    [TestMethod]
    public void WhenProjectsFilteredByName_ShouldReturnMatchingProject()
    {
        var query = $@"
            select p.Name, p.DefaultNamespace
            from #csharp.solution('{Solution1SolutionPath.Escape()}') s
            cross apply s.Projects p
            where p.Name = 'Solution1.ClassLibrary1.Tests'";

        var vm = CompileQuery(query);
        var result = vm.Run();

        Assert.AreEqual(1, result.Count,
            "Exactly one project named 'Solution1.ClassLibrary1.Tests' should be returned");
        Assert.AreEqual("Solution1.ClassLibrary1.Tests", result[0][0]?.ToString());
        Assert.AreEqual("Solution1.ClassLibrary1.Tests", result[0][1]?.ToString());
    }

    private CompiledQuery CompileQuery(string script)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            script,
            Guid.NewGuid().ToString(),
            new RoslynSchemaProvider((_, _) => new Mock<INuGetPropertiesResolver>().Object),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables(
                new Dictionary<string, string>
                {
                    { "MUSOQ_SERVER_HTTP_ENDPOINT", "https://localhost/internal/this-doesnt-exists" },
                    { "EXTERNAL_NUGET_PROPERTIES_RESOLVE_ENDPOINT", "https://localhost/external/this-doesnt-exists" }
                }));
    }
}
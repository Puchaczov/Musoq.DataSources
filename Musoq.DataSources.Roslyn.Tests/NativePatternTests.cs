using Microsoft.CodeAnalysis;
using Moq;
using Musoq.DataSources.Roslyn.CliCommands;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.Optimization;
namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
[DoNotParallelize]
public sealed class NativePatternTests
{
    [TestMethod]
    public void SolutionIdPatterns_FilterSourceRowsSeparatelyFromProjectCollections()
    {
        using var workspace = new AdhocWorkspace();
        workspace.AddProject("Alpha", LanguageNames.CSharp);
        workspace.AddProject("Beta", LanguageNames.CSharp);
        var root = Path.Combine(Path.GetTempPath(), "musoq-pattern-roslyn-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var key = Path.Combine(root, "Alpha.sln");
        var rates = SolutionOperationsCommand.RateLimitingOptions;
        SolutionOperationsCommand.RateLimitingOptions ??= new Dictionary<Roslyn.Components.NuGet.Http.Handlers.DomainRateLimitingHandler.DomainRateLimitingConfigKey, Roslyn.Components.NuGet.Http.Handlers.DomainRateLimitingHandler.DomainRateLimitConfig>();
        SolutionOperationsCommand.Solutions.TryAdd(key, workspace.CurrentSolution);
        try
        {
            var resolver = new Mock<INuGetPropertiesResolver>(MockBehavior.Strict);
            var schema = new CSharpSchema((_, _) => resolver.Object);
            var arguments = SourceArguments.Create(key, SourceArguments.Empty);
            var settings = new Dictionary<string, string> { ["MUSOQ_SERVER_HTTP_ENDPOINT"] = "https://localhost/unused", ["CACHE_DIRECTORY"] = root };
            PatternContractVerifier.VerifySource(schema, "solution", [key], context => NativeSourceCalls.Source(schema, arguments).Read<SolutionEntity>("solution", RuntimeV2TestContexts.CreateExecutionContext(sourceRuntimeSettings: settings, executionPlan: context.Plan)));
            var rowPattern = new SourcePredicateStringMatch(new("Id"), SourceStringMatchKind.Contains, "%", "");
            foreach (var restrictProjects in new[] { false, true })
            {
                SourcePredicateExpression predicate = restrictProjects
                    ? new SourcePredicateLogical(SourcePredicateLogicalOperator.And, rowPattern,
                        new SourcePredicateComparison(SourcePredicateComparisonOperator.Equal, new SourcePredicateColumn(new("p.Name")), new SourcePredicateLiteral("Alpha")))
                    : rowPattern;
                var plan = schema.TryPlanSource("solution", new() { Identity = new("csharp", "s", "s", "solution"), Predicate = predicate });
                var row = NativeSourceCalls.Source(schema, arguments).Read<SolutionEntity>("solution", RuntimeV2TestContexts.CreateExecutionContext(sourceRuntimeSettings: settings, executionPlan: plan.ExecutionPlan)).Chunks.SelectMany(chunk => chunk).Single();
                Assert.AreEqual(restrictProjects ? 1 : 2, row.Projects.Length);
                if (restrictProjects) Assert.AreEqual("Alpha", row.Projects.Single().Name);
            }
            resolver.VerifyNoOtherCalls();
        }
        finally
        {
            SolutionOperationsCommand.Solutions.TryRemove(key, out _);
            SolutionOperationsCommand.RateLimitingOptions = rates;
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void ProjectAndNestedPatterns_RemainResidual()
    {
        var schema = new CSharpSchema();
        foreach (var column in new[] { "AssemblyName", "p.Name", "Projects.Name", "Projects.AssemblyName" })
        {
            var predicate = new SourcePredicateRegexMatch(new(column), "Alpha", TimeSpan.FromMilliseconds(17));
            var plan = schema.TryPlanSource("solution", new() { Identity = new("csharp", "s", "s", "solution"), Predicate = predicate, Take = 1 });
            Assert.AreSame(predicate, plan.ResidualPredicate);
            Assert.AreEqual(0, plan.ExecutionPlan.PredicateApplications.Count);
            Assert.IsNull(plan.AcceptedTake);
        }
    }
}

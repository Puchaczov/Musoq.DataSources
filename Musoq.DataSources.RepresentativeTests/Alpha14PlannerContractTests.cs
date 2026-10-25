using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Archives;
using Musoq.DataSources.CANBus;
using Musoq.DataSources.Docker;
using Musoq.DataSources.Git;
using Musoq.DataSources.GitHub;
using Musoq.DataSources.Jira;
using Musoq.DataSources.Os;
using Musoq.DataSources.Roslyn;
using Musoq.DataSources.Search;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.RepresentativeTests;

[TestClass]
public sealed class Alpha14PlannerContractTests
{
    [DataTestMethod]
    [DataRow("archives", "file", "Key")]
    [DataRow("docker", "containers", "Image")]
    [DataRow("docker", "images", "ID")]
    [DataRow("docker", "networks", "Name")]
    [DataRow("docker", "volumes", "Name")]
    [DataRow("git", "commits", "Author")]
    [DataRow("git", "branches", "FriendlyName")]
    [DataRow("github", "issues", "State")]
    [DataRow("github", "pullrequests", "HeadRef")]
    [DataRow("github", "commits", "AuthorLogin")]
    [DataRow("github", "repositories", "Language")]
    [DataRow("jira", "issues", "Status")]
    [DataRow("can", "messages", "Name")]
    [DataRow("can", "signals", "MessageName")]
    [DataRow("os", "files", "Name")]
    [DataRow("os", "directories", "Name")]
    [DataRow("roslyn", "solution", "Id")]
    [DataRow("search", "paths", "Path")]
    public void ExistingStringPredicateColumns_AcceptExactRegexApplications(string provider, string source, string column)
    {
        var schema = Schema(provider);
        foreach (var negated in new[] { false, true })
        {
            var regex = new SourcePredicateRegexMatch(new SourceColumnRef(column), "(?i)^a", TimeSpan.FromMilliseconds(17), isNegated: negated);
            var request = new SourcePlanRequest
            {
                Identity = new SourceIdentity(provider, "s", "s", source), Predicate = regex,
                OrderBy = [new OrderByExpression(new SourceColumnRef(column), OrderDirection.Ascending)], Skip = 1, Take = 2
            };
            var result = schema.TryPlanSource(source, request);
            Assert.AreSame(regex, result.AcceptedPredicate);
            Assert.IsNull(result.ResidualPredicate);
            Assert.AreSame(regex, result.ExecutionPlan.PredicateApplications.Single().Predicate);
            Assert.AreEqual(SourcePredicateEvaluationPhase.RowFiltering, result.ExecutionPlan.PredicateApplications.Single().Phase);
            Assert.IsNull(result.AcceptedSkip);
            Assert.IsNull(result.AcceptedTake);
            var unsupported = new SourcePredicateRegexMatch(new SourceColumnRef("NotAnEligibleStringColumn"), regex.Pattern, regex.MatchTimeout, isNegated: negated);
            var rejected = schema.TryPlanSource(source, request with { Predicate = unsupported });
            Assert.IsNull(rejected.AcceptedPredicate);
            Assert.AreSame(unsupported, rejected.ResidualPredicate);
        }
    }

    private static ISchema Schema(string name) => name switch
    {
        "archives" => new ArchivesSchema(), "docker" => new DockerSchema(), "git" => new GitSchema(),
        "github" => new GitHubSchema(), "jira" => new JiraSchema(), "can" => new CANBusSchema(),
        "os" => new OsSchema(), "roslyn" => new CSharpSchema(), "search" => new SearchSchema(),
        _ => throw new NotSupportedException(name)
    };
}

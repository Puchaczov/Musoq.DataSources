using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Jira.Entities;
using Musoq.DataSources.Jira.Tests.TestHelpers;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Jira.Tests;

[TestClass]
public sealed class JiraStarContractTests
{
    private static readonly StarContractCase[] Cases =
    [
        new(
            "issues",
            [typeof(string)],
            ["TEST"],
            "select * from jira.issues('TEST')",
            [
                Column("Key", typeof(string)), Column("Id", typeof(string)), Column("Summary", typeof(string)),
                Column("Description", typeof(string)), Column("Type", typeof(string)), Column("Status", typeof(string)),
                Column("Priority", typeof(string)), Column("Resolution", typeof(string)), Column("Assignee", typeof(string)),
                Column("AssigneeDisplayName", typeof(string)), Column("Reporter", typeof(string)),
                Column("ReporterDisplayName", typeof(string)), Column("ProjectKey", typeof(string)),
                Column("CreatedAt", typeof(DateTimeOffset?)), Column("UpdatedAt", typeof(DateTimeOffset?)),
                Column("ResolvedAt", typeof(DateTimeOffset?)), Column("DueDate", typeof(DateTime?)),
                Column("Labels", typeof(string)), Column("Components", typeof(string)), Column("FixVersions", typeof(string)),
                Column("AffectsVersions", typeof(string)), Column("OriginalEstimateSeconds", typeof(long?)),
                Column("RemainingEstimateSeconds", typeof(long?)), Column("TimeSpentSeconds", typeof(long?)),
                Column("OriginalEstimate", typeof(string)), Column("RemainingEstimate", typeof(string)),
                Column("TimeSpent", typeof(string)), Column("ParentKey", typeof(string)), Column("Environment", typeof(string)),
                Column("Votes", typeof(long?)), Column("SecurityLevel", typeof(string)), Column("Url", typeof(string))
            ],
            []),
        new(
            "projects",
            [],
            [],
            "select * from jira.projects()",
            [
                Column("Id", typeof(string)), Column("Key", typeof(string)), Column("Name", typeof(string)),
                Column("Description", typeof(string)), Column("Lead", typeof(string)), Column("Url", typeof(string)),
                Column("Category", typeof(string)), Column("CategoryDescription", typeof(string)),
                Column("AvatarUrl", typeof(string))
            ],
            []),
        new(
            "comments",
            [typeof(string)],
            ["TEST-123"],
            "select * from jira.comments('TEST-123')",
            [
                Column("Id", typeof(string)), Column("IssueKey", typeof(string)), Column("Body", typeof(string)),
                Column("Author", typeof(string)), Column("AuthorDisplayName", typeof(string)),
                Column("UpdateAuthor", typeof(string)), Column("UpdateAuthorDisplayName", typeof(string)),
                Column("CreatedAt", typeof(DateTimeOffset?)), Column("UpdatedAt", typeof(DateTimeOffset?)),
                Column("VisibilityGroup", typeof(string)), Column("VisibilityRole", typeof(string))
            ],
            [])
    ];

    [TestMethod]
    public void EveryJiraConstructor_HasOneExactStarContract()
    {
        var api = CreateApi();
        var schema = new JiraSchema(api.Object);
        var context = CreateMetadataContext();

        StarContractAssertions.AssertConstructors(schema.GetRawConstructors(context), Cases);

        foreach (var contract in Cases)
        {
            var table = schema.GetTableByName(contract.MethodName, context, contract.Arguments.ToArray());
            StarContractAssertions.AssertExcludedColumnsRemainInSchema(table, contract);

            var result = Compile(contract.Query, api.Object).Run();
            StarContractAssertions.AssertResult(result, contract);
        }
    }

    private static Mock<IJiraApi> CreateApi()
    {
        var api = new Mock<IJiraApi>();
        api.Setup(value => value.GetIssuesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync([MockEntityFactory.CreateIssue()]);
        api.Setup(value => value.GetProjectsAsync())
            .ReturnsAsync([MockEntityFactory.CreateProject()]);
        api.Setup(value => value.GetCommentsAsync(It.IsAny<string>()))
            .ReturnsAsync([MockEntityFactory.CreateComment()]);
        return api;
    }

    private static CompiledQuery Compile(string query, IJiraApi api)
    {
        var provider = new Mock<ISchemaProvider>();
        provider.Setup(value => value.GetSchema(It.IsAny<string>())).Returns(new JiraSchema(api));

        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            provider.Object,
            new Dictionary<uint, IReadOnlyDictionary<string, string>>
            {
                [0] = new Dictionary<string, string>
                {
                    ["JIRA_URL"] = "https://test.atlassian.net",
                    ["JIRA_USERNAME"] = "test@example.com",
                    ["JIRA_API_TOKEN"] = "test_token"
                }
            });
    }

    private static SourceMetadataContext CreateMetadataContext()
    {
        return new SourceMetadataContext(
            "jira-star-contract",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            NullLogger.Instance);
    }

    private static StarContractColumn Column(string name, Type type) => new(name, type);
}

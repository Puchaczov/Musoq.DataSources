using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Musoq.Schema.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator.Exceptions;
using Musoq.Parser.Diagnostics;
using Musoq.Schema;
using Octokit;

namespace Musoq.DataSources.GitHub.Tests;

[TestClass]
public sealed class GitHubDiagnosticTests
{
    [DataTestMethod]
    [DataRow("repositories()")]
    [DataRow("issues('owner', 'repo')")]
    [DataRow("pullrequests('owner', 'repo')")]
    [DataRow("commits('owner', 'repo')")]
    [DataRow("branches('owner', 'repo')")]
    [DataRow("branchcommits('owner', 'repo', 'main', 'feature')")]
    [DataRow("releases('owner', 'repo')")]
    public void DeferredSdkFailure_ReportsEverySourceWithoutPrivateBody(string source)
    {
        var cause = new ApiException("PRIVATE-GITHUB-BODY", HttpStatusCode.Forbidden);
        var api = new Mock<IGitHubApi>(MockBehavior.Strict);
        api.Setup(x => x.GetUserRepositoriesAsync(It.IsAny<RepositoryRequest>(), It.IsAny<int?>(), It.IsAny<int?>())).ThrowsAsync(cause);
        api.Setup(x => x.GetIssuesAsync("owner", "repo", It.IsAny<RepositoryIssueRequest>(), It.IsAny<int?>(), It.IsAny<int?>())).ThrowsAsync(cause);
        api.Setup(x => x.GetPullRequestsAsync("owner", "repo", It.IsAny<PullRequestRequest>(), It.IsAny<int?>(), It.IsAny<int?>())).ThrowsAsync(cause);
        api.Setup(x => x.GetCommitsAsync("owner", "repo", It.IsAny<CommitRequest>(), It.IsAny<int?>(), It.IsAny<int?>())).ThrowsAsync(cause);
        api.Setup(x => x.GetBranchesAsync("owner", "repo", It.IsAny<int?>(), It.IsAny<int?>())).ThrowsAsync(cause);
        api.Setup(x => x.GetBranchSpecificCommitsAsync("owner", "repo", "main", "feature")).ThrowsAsync(cause);
        api.Setup(x => x.GetReleasesAsync("owner", "repo", It.IsAny<int?>(), It.IsAny<int?>())).ThrowsAsync(cause);
        using var compiled = InstanceCreatorHelpers.CompileForExecution($"select * from github.{source} g",
            Guid.NewGuid().ToString("N"), new Provider(api.Object), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        var error = Assert.ThrowsException<QueryExecutionException>(() => _ = compiled.Run().Count);
        Assert.AreEqual(DiagnosticCode.MQ7020_ReportedDataSourceFailure, error.Envelope!.Code);
        Assert.AreEqual("GITHUB.AccessDenied", error.Envelope.Arguments["providerCode"]);
        Assert.AreEqual("false", error.Envelope.Arguments["canRetry"]);
        Assert.IsFalse(MusoqErrorEnvelopeFormatter.FormatJson(error.Envelope).Contains("PRIVATE-GITHUB-BODY", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task OrganizationLookupFallback_ReportsWarningsToEachExecutionOnly()
    {
        var client = new Mock<IGitHubClient> { DefaultValue = DefaultValue.Mock };
        client.Setup(x => x.Repository.GetAllForOrg("owner", It.IsAny<ApiOptions>())).ThrowsAsync(new NotFoundException("PRIVATE-OWNER", HttpStatusCode.NotFound));
        client.Setup(x => x.Repository.GetAllForUser("owner", It.IsAny<ApiOptions>())).ReturnsAsync(Array.Empty<Repository>());
        var api = new GitHubApi(client.Object);
        var first = new List<DataSourceDiagnostic>();
        var second = new List<DataSourceDiagnostic>();
        await api.WithReporting(first.Add, CancellationToken.None).GetRepositoriesForOwnerAsync("owner");
        Assert.AreEqual(0, second.Count);
        await api.WithReporting(second.Add, CancellationToken.None).GetRepositoriesForOwnerAsync("owner");
        Assert.AreEqual("GITHUB.ResourceNotFound.Fallback", first.Single().Code);
        Assert.AreEqual("GITHUB.ResourceNotFound.Fallback", second.Single().Code);
        Assert.IsFalse(first.Single().Message.Contains("PRIVATE-OWNER", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task PullRequestDetailFailure_RetainsLightweightRowAndReportsWarning()
    {
        var client = new Mock<IGitHubClient> { DefaultValue = DefaultValue.Mock };
        client.Setup(x => x.PullRequest.GetAllForRepository("owner", "repo", It.IsAny<PullRequestRequest>(), It.IsAny<ApiOptions>()))
            .ReturnsAsync(new[] { new PullRequest() });
        client.Setup(x => x.PullRequest.Get("owner", "repo", 0)).ThrowsAsync(new ApiException("PRIVATE-DETAIL", HttpStatusCode.ServiceUnavailable));
        var warnings = new List<DataSourceDiagnostic>();
        var rows = await new GitHubApi(client.Object).WithReporting(warnings.Add, CancellationToken.None).GetPullRequestsAsync("owner", "repo");
        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual("GITHUB.Unavailable.Fallback", warnings.Single().Code);
    }

    [TestMethod]
    public void PrimaryRateLimit_IsNotMisclassifiedAsAccessDenied()
    {
        var response = Response();
        response.SetupGet(x => x.ApiInfo).Returns(new ApiInfo(new Dictionary<string, Uri>(), [], [], "", new RateLimit(100, 0, 0)));
        var error = GitHubDiagnostics.Map(new RateLimitExceededException(response.Object))!;
        Assert.AreEqual(DataSourceFailureKind.RateLimited, error.Report.FailureKind);
        Assert.AreEqual(TimeSpan.Zero, error.Report.RetryAfter);
        Assert.IsTrue(error.Report.CanRetry);
    }

    [TestMethod]
    public void SecondaryRateLimit_PreservesSdkRetryDelay()
    {
        var response = Response();
        response.SetupGet(x => x.Headers).Returns(new Dictionary<string, string> { ["Retry-After"] = "30" });
        var error = GitHubDiagnostics.Map(new AbuseException(response.Object))!;
        Assert.AreEqual(DataSourceFailureKind.RateLimited, error.Report.FailureKind);
        Assert.AreEqual(TimeSpan.FromSeconds(30), error.Report.RetryAfter);
    }

    [TestMethod]
    public void FailureAfterFirstPage_StillFailsTheExecution()
    {
        var api = new Mock<IGitHubApi>(MockBehavior.Strict);
        var row = TestHelpers.MockEntityFactory.CreateRepository();
        api.SetupSequence(x => x.GetUserRepositoriesAsync(It.IsAny<RepositoryRequest>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(Enumerable.Repeat(row, 100).ToArray())
            .ThrowsAsync(new ApiException("PRIVATE-SECOND-PAGE", HttpStatusCode.ServiceUnavailable));
        using var compiled = InstanceCreatorHelpers.CompileForExecution("select * from github.repositories() g",
            Guid.NewGuid().ToString("N"), new Provider(api.Object), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        var error = Assert.ThrowsException<QueryExecutionException>(() => _ = compiled.Run().Count);
        Assert.AreEqual("GITHUB.Unavailable", error.Envelope!.Arguments["providerCode"]);
        Assert.AreEqual(2, api.Invocations.Count);
    }

    private static Mock<IResponse> Response()
    {
        var response = new Mock<IResponse>();
        response.SetupGet(x => x.StatusCode).Returns(HttpStatusCode.Forbidden);
        response.SetupGet(x => x.Headers).Returns(new Dictionary<string, string>());
        response.SetupGet(x => x.Body).Returns("{\"message\":\"PRIVATE-BODY\"}");
        return response;
    }

    private sealed class Provider(IGitHubApi api) : ISchemaProvider
    {
        public ISchema GetSchema(string _) => new GitHubSchema(api);
    }
}

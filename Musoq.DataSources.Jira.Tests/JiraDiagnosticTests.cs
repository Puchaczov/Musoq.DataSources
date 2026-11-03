using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Musoq.Schema.Diagnostics;
using System.Security.Authentication;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator.Exceptions;
using Musoq.Parser.Diagnostics;
using Musoq.Schema;

namespace Musoq.DataSources.Jira.Tests;

[TestClass]
public sealed class JiraDiagnosticTests
{
    [DataTestMethod]
    [DataRow("projects()")]
    [DataRow("issues('project = TEST')")]
    [DataRow("comments('TEST-1')")]
    public void DeferredAuthenticationFailure_ReportsEverySourceWithoutSdkBody(string source)
    {
        var cause = new AuthenticationException("PRIVATE-JIRA-BODY");
        var api = new Mock<IJiraApi>(MockBehavior.Strict);
        api.Setup(x => x.GetProjectsAsync()).ThrowsAsync(cause);
        api.Setup(x => x.GetIssuesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>())).ThrowsAsync(cause);
        api.Setup(x => x.GetCommentsAsync("TEST-1")).ThrowsAsync(cause);
        using var compiled = InstanceCreatorHelpers.CompileForExecution($"select * from jira.{source} j",
            Guid.NewGuid().ToString("N"), new Provider(api.Object), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        var error = Assert.ThrowsException<QueryExecutionException>(() => _ = compiled.Run().Count);
        Assert.AreEqual(DiagnosticCode.MQ7020_ReportedDataSourceFailure, error.Envelope!.Code);
        Assert.AreEqual("JIRA.AccessDenied", error.Envelope.Arguments["providerCode"]);
        Assert.IsFalse(MusoqErrorEnvelopeFormatter.FormatJson(error.Envelope).Contains("PRIVATE-JIRA-BODY", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MessageOnlySdkStatus_RemainsUnclassified()
        => Assert.IsNull(JiraDiagnostics.Map(new InvalidOperationException("Response Status Code: 429 PRIVATE-JIRA-BODY")));

    [TestMethod]
    public async Task MissingOptionalProject_RetainsNullAndReportsExecutionLocalWarning()
    {
        var projects = new Mock<Atlassian.Jira.IProjectService>();
        projects.Setup(x => x.GetProjectAsync("PRIVATE-PROJECT", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Atlassian.Jira.Remote.ResourceNotFoundException("PRIVATE-BODY"));
        var services = new Atlassian.Jira.ServiceLocator();
        services.Register(() => projects.Object);
        var warnings = new List<DataSourceDiagnostic>();
        var api = new JiraApi(new Atlassian.Jira.Jira(services)).WithReporting(warnings.Add, CancellationToken.None);
        Assert.IsNull(await api.GetProjectAsync("PRIVATE-PROJECT"));
        Assert.AreEqual("JIRA.ResourceNotFound.Fallback", warnings.Single().Code);
        Assert.IsFalse(warnings.Single().Message.Contains("PRIVATE", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task OptionalProjectDefect_IsNotSuppressed()
    {
        var projects = new Mock<Atlassian.Jira.IProjectService>();
        var cause = new InvalidOperationException("PRIVATE-DEFECT");
        projects.Setup(x => x.GetProjectAsync("project", It.IsAny<CancellationToken>())).ThrowsAsync(cause);
        var services = new Atlassian.Jira.ServiceLocator();
        services.Register(() => projects.Object);
        var api = new JiraApi(new Atlassian.Jira.Jira(services));
        Assert.AreSame(cause, await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => api.GetProjectAsync("project")));
    }

    private sealed class Provider(IJiraApi api) : ISchemaProvider
    {
        public ISchema GetSchema(string _) => new JiraSchema(api);
    }
}

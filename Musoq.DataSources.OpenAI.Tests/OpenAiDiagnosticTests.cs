#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.DataSources.OpenAI.Tests.Components;
using Musoq.Evaluator;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.OpenAI.Tests;

[TestClass]
public sealed class OpenAiDiagnosticTests
{
    [TestMethod]
    public void RecognizedCompletionFailure_UsesSafeFallbackAndNonThrowingWarning()
    {
        var warnings = new List<DataSourceDiagnostic>();
        var api = new Mock<IOpenAiApi>();
        api.Setup(x => x.GetCompletionAsync(It.IsAny<OpenAiEntityBase>(), It.IsAny<IList<global::OpenAI.Chat.ChatMessage>>()))
            .ThrowsAsync(new HttpRequestException("PRIVATE-RESPONSE-BODY", null, HttpStatusCode.TooManyRequests));
        var entity = Entity(api, warnings.Add);
        var result = new OpenAiLibrary().SummarizeContent(entity, "PRIVATE-PROMPT");
        Assert.AreEqual("Completion unavailable. See datasource warnings.", result);
        var report = System.Linq.Enumerable.Single(warnings);
        Assert.AreEqual("OPENAI.RateLimited.Fallback", report.Code);
        Assert.AreEqual(DataSourceDiagnosticSeverity.Warning, report.Severity);
        Assert.IsTrue(report.CanRetry);
        Assert.IsFalse(report.Message.Contains("PRIVATE", StringComparison.Ordinal));
    }

    [TestMethod]
    public void UnexpectedDefect_IsNotConvertedToSuccessfulText()
    {
        var api = new Mock<IOpenAiApi>();
        var cause = new InvalidOperationException("PRIVATE-DEFECT");
        api.Setup(x => x.GetCompletionAsync(It.IsAny<OpenAiEntityBase>(), It.IsAny<IList<global::OpenAI.Chat.ChatMessage>>())).ThrowsAsync(cause);
        var error = Assert.ThrowsException<InvalidOperationException>(() => new OpenAiLibrary().SummarizeContent(Entity(api), "text"));
        Assert.AreSame(cause, error);
    }

    [TestMethod]
    public void CallerCancellation_IsNotACompletionFallback()
    {
        var api = new Mock<IOpenAiApi>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => new OpenAiLibrary().SummarizeContent(Entity(api, token: cancellation.Token), "text"));
        api.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void TolerantMalformedOutput_ReportsWarningAndRetainsEmptyResult()
    {
        var warnings = new List<DataSourceDiagnostic>();
        var api = new Mock<IOpenAiApi>();
        api.Setup(x => x.GetCompletionAsync(It.IsAny<OpenAiEntityBase>(), It.IsAny<IList<global::OpenAI.Chat.ChatMessage>>()))
            .ReturnsAsync(new CompletionResponse("PRIVATE-MALFORMED-OUTPUT"));
        Assert.AreEqual(0, new OpenAiLibrary().Entities(Entity(api, warnings.Add), "text").Length);
        var report = System.Linq.Enumerable.Single(warnings);
        Assert.AreEqual("OPENAI.InvalidModelOutput.Fallback", report.Code);
        Assert.IsFalse(report.Message.Contains("PRIVATE", StringComparison.Ordinal));
    }

    [TestMethod]
    public void StrictMalformedOutput_ReportsErrorAndRetainsPrivateCause()
    {
        var api = new Mock<IOpenAiApi>();
        api.Setup(x => x.GetCompletionAsync(It.IsAny<OpenAiEntityBase>(), It.IsAny<IList<global::OpenAI.Chat.ChatMessage>>()))
            .ReturnsAsync(new CompletionResponse("PRIVATE-MALFORMED-OUTPUT"));
        var error = Assert.ThrowsException<DataSourceException>(() => new OpenAiLibrary().Entities(Entity(api), "text", true));
        Assert.AreEqual("OPENAI.InvalidModelOutput", error.Report.Code);
        Assert.IsNotNull(error.InnerException);
        Assert.IsFalse(error.Report.Message.Contains("PRIVATE", StringComparison.Ordinal));
    }

    [DataTestMethod]
    [DataRow(400, DataSourceFailureKind.InvalidInput)]
    [DataRow(401, DataSourceFailureKind.AccessDenied)]
    [DataRow(404, DataSourceFailureKind.ResourceNotFound)]
    [DataRow(408, DataSourceFailureKind.Timeout)]
    [DataRow(429, DataSourceFailureKind.RateLimited)]
    [DataRow(503, DataSourceFailureKind.Unavailable)]
    public void ConcreteSdkStatus_IsClassifiedWithoutResponseText(int status, DataSourceFailureKind kind)
    {
        var response = new Mock<global::System.ClientModel.Primitives.PipelineResponse>();
        response.SetupGet(x => x.Status).Returns(status);
        var cause = new global::System.ClientModel.ClientResultException("PRIVATE-SDK-BODY", response.Object);
        var error = OpenAiDiagnostics.Map(cause)!;
        Assert.AreEqual(kind, error.Report.FailureKind);
        Assert.AreSame(cause, error.InnerException);
        Assert.IsFalse(error.Report.Message.Contains("PRIVATE", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CompiledQuery_ReturnsResultsAndExecutionWarningsTogether()
    {
        var api = new Mock<IOpenAiApi>();
        api.Setup(x => x.GetCompletionAsync(It.IsAny<OpenAiEntityBase>(), It.IsAny<IList<global::OpenAI.Chat.ChatMessage>>()))
            .ThrowsAsync(new HttpRequestException("PRIVATE-SERVICE-BODY", null, HttpStatusCode.ServiceUnavailable));
        using var compiled = InstanceCreatorHelpers.CompileForExecution("select SummarizeContent('text') from openai.gpt() s",
            Guid.NewGuid().ToString("N"), new TestsOpenAiSchemaProvider(api.Object), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        var warnings = new DataSourceWarningCollector();
        Assert.AreEqual(1, compiled.Run(new TypedQueryRunOptions { DataSourceWarnings = warnings }).Count);
        var report = System.Linq.Enumerable.Single(warnings.Snapshot());
        Assert.AreEqual("OPENAI.Unavailable.Fallback", report.Arguments["providerCode"]);
        Assert.AreEqual("s", report.Arguments["alias"]);
        Assert.IsFalse(report.Message.Contains("PRIVATE", StringComparison.Ordinal));
    }

    private static OpenAiEntity Entity(Mock<IOpenAiApi> api, Action<DataSourceDiagnostic>? reporter = null, CancellationToken token = default)
        => new(api.Object, "model", 0, 100, 0, 0, token) { WarningReporter = reporter };
}

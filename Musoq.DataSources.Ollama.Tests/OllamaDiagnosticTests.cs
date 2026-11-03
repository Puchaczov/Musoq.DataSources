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
using Musoq.DataSources.Ollama.Tests.Components;
using Musoq.Evaluator;
using Musoq.Schema.Diagnostics;

namespace Musoq.DataSources.Ollama.Tests;

[TestClass]
public sealed class OllamaDiagnosticTests
{
    [TestMethod]
    public void RecognizedCompletionFailure_UsesSafeFallbackAndNonThrowingWarning()
    {
        var warnings = new List<DataSourceDiagnostic>();
        var api = new Mock<IOllamaApi>();
        api.Setup(x => x.GetCompletionAsync(It.IsAny<OllamaEntityBase>(), It.IsAny<IList<OllamaSharp.Models.Chat.Message>>()))
            .ThrowsAsync(new HttpRequestException("PRIVATE-RESPONSE-BODY", null, HttpStatusCode.TooManyRequests));
        var entity = Entity(api, warnings.Add);
        var result = new OllamaLibrary().SummarizeContent(entity, "PRIVATE-PROMPT");
        Assert.AreEqual("Completion unavailable. See datasource warnings.", result);
        var report = System.Linq.Enumerable.Single(warnings);
        Assert.AreEqual("OLLAMA.RateLimited.Fallback", report.Code);
        Assert.AreEqual(DataSourceDiagnosticSeverity.Warning, report.Severity);
        Assert.IsTrue(report.CanRetry);
        Assert.IsFalse(report.Message.Contains("PRIVATE", StringComparison.Ordinal));
    }

    [TestMethod]
    public void UnexpectedDefect_IsNotConvertedToSuccessfulText()
    {
        var api = new Mock<IOllamaApi>();
        var cause = new InvalidOperationException("PRIVATE-DEFECT");
        api.Setup(x => x.GetCompletionAsync(It.IsAny<OllamaEntityBase>(), It.IsAny<IList<OllamaSharp.Models.Chat.Message>>())).ThrowsAsync(cause);
        var error = Assert.ThrowsException<InvalidOperationException>(() => new OllamaLibrary().SummarizeContent(Entity(api), "text"));
        Assert.AreSame(cause, error);
    }

    [TestMethod]
    public void CallerCancellation_IsNotACompletionFallback()
    {
        var api = new Mock<IOllamaApi>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => new OllamaLibrary().SummarizeContent(Entity(api, token: cancellation.Token), "text"));
        api.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void TolerantMalformedOutput_ReportsWarningAndRetainsEmptyResult()
    {
        var warnings = new List<DataSourceDiagnostic>();
        var api = new Mock<IOllamaApi>();
        api.Setup(x => x.GetCompletionAsync(It.IsAny<OllamaEntityBase>(), It.IsAny<IList<OllamaSharp.Models.Chat.Message>>()))
            .ReturnsAsync(new CompletionResponse("PRIVATE-MALFORMED-OUTPUT"));
        Assert.AreEqual(0, new OllamaLibrary().Entities(Entity(api, warnings.Add), "text").Length);
        var report = System.Linq.Enumerable.Single(warnings);
        Assert.AreEqual("OLLAMA.InvalidModelOutput.Fallback", report.Code);
        Assert.IsFalse(report.Message.Contains("PRIVATE", StringComparison.Ordinal));
    }

    [TestMethod]
    public void StrictMalformedOutput_ReportsErrorAndRetainsPrivateCause()
    {
        var api = new Mock<IOllamaApi>();
        api.Setup(x => x.GetCompletionAsync(It.IsAny<OllamaEntityBase>(), It.IsAny<IList<OllamaSharp.Models.Chat.Message>>()))
            .ReturnsAsync(new CompletionResponse("PRIVATE-MALFORMED-OUTPUT"));
        var error = Assert.ThrowsException<DataSourceException>(() => new OllamaLibrary().Entities(Entity(api), "text", true));
        Assert.AreEqual("OLLAMA.InvalidModelOutput", error.Report.Code);
        Assert.IsNotNull(error.InnerException);
        Assert.IsFalse(error.Report.Message.Contains("PRIVATE", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CancellationDuringRequest_IsNeverRetriedOrReportedAsTimeout()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new CancelRequestHandler(cancellation);
        using var client = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);
        var api = new OllamaApi("http://localhost:11434", factory.Object);
        var warnings = new List<DataSourceDiagnostic>();
        var entity = new OllamaEntity(api, "model", 0, cancellation.Token) { WarningReporter = warnings.Add };
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => api.GetCompletionAsync(entity, []));
        Assert.AreEqual(1, handler.Requests);
        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    public async Task CancellationBeforeRetryWait_StopsWithoutAnotherRequest()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new UnavailableHandler();
        using var client = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);
        var api = new OllamaApi("http://localhost:11434", factory.Object);
        var warnings = new List<DataSourceDiagnostic>();
        var entity = new OllamaEntity(api, "model", 0, cancellation.Token)
        { WarningReporter = report => { warnings.Add(report); cancellation.Cancel(); } };
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => api.GetCompletionAsync(entity, []));
        Assert.AreEqual(1, handler.Requests);
        Assert.AreEqual("OLLAMA.Unavailable.Fallback", System.Linq.Enumerable.Single(warnings).Code);
    }

    private sealed class CancelRequestHandler(CancellationTokenSource cancellation) : HttpMessageHandler
    {
        internal int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests++;
            cancellation.Cancel();
            return Task.FromCanceled<HttpResponseMessage>(token);
        }
    }

    private sealed class UnavailableHandler : HttpMessageHandler
    {
        internal int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests++;
            throw new HttpRequestException("PRIVATE-SERVICE-BODY", null, HttpStatusCode.ServiceUnavailable);
        }
    }

    [TestMethod]
    public void CompiledQuery_ReturnsResultsAndExecutionWarningsTogether()
    {
        var api = new Mock<IOllamaApi>();
        api.Setup(x => x.GetCompletionAsync(It.IsAny<OllamaEntityBase>(), It.IsAny<IList<global::OllamaSharp.Models.Chat.Message>>()))
            .ThrowsAsync(new HttpRequestException("PRIVATE-SERVICE-BODY", null, HttpStatusCode.ServiceUnavailable));
        using var compiled = InstanceCreatorHelpers.CompileForExecution("select SummarizeContent('text') from ollama.llm() s",
            Guid.NewGuid().ToString("N"), new TestsOllamaSchemaProvider(api.Object), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        var warnings = new DataSourceWarningCollector();
        Assert.AreEqual(1, compiled.Run(new TypedQueryRunOptions { DataSourceWarnings = warnings }).Count);
        var report = System.Linq.Enumerable.Single(warnings.Snapshot());
        Assert.AreEqual("OLLAMA.Unavailable.Fallback", report.Arguments["providerCode"]);
        Assert.AreEqual("s", report.Arguments["alias"]);
        Assert.IsFalse(report.Message.Contains("PRIVATE", StringComparison.Ordinal));
    }

    private static OllamaEntity Entity(Mock<IOllamaApi> api, Action<DataSourceDiagnostic>? reporter = null, CancellationToken token = default)
        => new(api.Object, "model", 0, token) { WarningReporter = reporter };
}

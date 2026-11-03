using System;
using System.Net;
using Docker.DotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator.Exceptions;
using Musoq.Parser.Diagnostics;
using Musoq.Schema;

namespace Musoq.DataSources.Docker.Tests;

[TestClass]
public sealed class DockerDiagnosticTests
{
    [DataTestMethod]
    [DataRow("containers")]
    [DataRow("images")]
    [DataRow("networks")]
    [DataRow("volumes")]
    public void DeferredSdkFailure_ReportsEverySourceWithoutResponseBody(string source)
    {
        var cause = new DockerApiException(HttpStatusCode.ServiceUnavailable, "PRIVATE-DOCKER-BODY");
        var api = new Mock<IDockerApi>(MockBehavior.Strict);
        api.Setup(x => x.ListContainersAsync()).ThrowsAsync(cause);
        api.Setup(x => x.ListImagesAsync()).ThrowsAsync(cause);
        api.Setup(x => x.ListNetworksAsync()).ThrowsAsync(cause);
        api.Setup(x => x.ListVolumesAsync()).ThrowsAsync(cause);
        using var compiled = InstanceCreatorHelpers.CompileForExecution($"select * from docker.{source}() d",
            Guid.NewGuid().ToString("N"), new Provider(api.Object), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        var error = Assert.ThrowsException<QueryExecutionException>(() => _ = compiled.Run().Count);
        Assert.AreEqual(DiagnosticCode.MQ7020_ReportedDataSourceFailure, error.Envelope!.Code);
        Assert.AreEqual("DOCKER.Unavailable", error.Envelope.Arguments["providerCode"]);
        Assert.AreEqual("true", error.Envelope.Arguments["canRetry"]);
        Assert.IsFalse(MusoqErrorEnvelopeFormatter.FormatJson(error.Envelope).Contains("PRIVATE-DOCKER-BODY", StringComparison.Ordinal));
    }

    private sealed class Provider(IDockerApi api) : ISchemaProvider
    {
        public ISchema GetSchema(string _) => new DockerSchema(api);
    }
}

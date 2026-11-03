using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.CANBus;
using Musoq.DataSources.FlatFile;
using Musoq.DataSources.RepresentativeTests.Components;
using Musoq.DataSources.System;
using Musoq.DataSources.Tests.Common;
using Musoq.Converter.Exceptions;
using Musoq.Evaluator;
using Musoq.Evaluator.Exceptions;
using Musoq.Parser.Diagnostics;
using Musoq.Schema;
using Musoq.Schema.Diagnostics;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.RepresentativeTests;

[TestClass]
public sealed class DatasourceDiagnosticIntegrationTests
{
    [DataTestMethod]
    [DataRow("archives.file('{0}')", "ARCHIVES.ResourceNotFound")]
    [DataRow("can.messages('{0}')", "CANBUS.ResourceNotFound")]
    [DataRow("json.file('{0}')", "JSON.ResourceNotFound")]
    [DataRow("separatedvalues.comma('{0}', true, 0)", "SEPARATEDVALUES.ResourceNotFound")]
    [DataRow("git.commits('{0}')", "GIT.ResourceNotFound")]
    [DataRow("csharp.solution('{0}')", "ROSLYN.ResourceNotFound")]
    [DataRow("search.matches('{0}', 'text')", "SEARCH-SOURCE-001")]
    public void MissingResource_ReportsProviderIdentityAndSafePublicGuidance(string source, string providerCode)
    {
        var path = MissingPath();
        var envelope = Failure(string.Format(global::System.Globalization.CultureInfo.InvariantCulture, source, Escape(path)));
        Assert.AreEqual(DiagnosticCode.MQ7020_ReportedDataSourceFailure, envelope.Code);
        Assert.AreEqual(providerCode, envelope.Arguments["providerCode"]);
        Assert.AreEqual("ResourceNotFound", envelope.Arguments["failureKind"]);
        Assert.IsTrue(envelope.Arguments.ContainsKey("sourceContextId"));
        Assert.IsTrue(envelope.Arguments.ContainsKey("operation"));
        Assert.IsTrue(envelope.Arguments.ContainsKey("source"));
        Assert.IsFalse(MusoqErrorEnvelopeFormatter.FormatJson(envelope).Contains(path, StringComparison.Ordinal));
    }

    [TestMethod]
    public void InvalidArchive_ReportsFormatWithoutEchoingContents()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "PRIVATE-INVALID-ARCHIVE");
            var envelope = Failure($"archives.file('{Escape(path)}')");
            Assert.AreEqual("ARCHIVES.InvalidArchive", envelope.Arguments["providerCode"]);
            Assert.AreEqual("InvalidInput", envelope.Arguments["failureKind"]);
            Assert.IsFalse(MusoqErrorEnvelopeFormatter.FormatJson(envelope).Contains("PRIVATE-INVALID-ARCHIVE", StringComparison.Ordinal));
        }
        finally { File.Delete(path); }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FlatFileMissingInput_RemainsEmptyAndReportsExecutionLocalWarning(bool usePattern)
    {
        var warnings = new DataSourceWarningCollector();
        using var compiled = Compile($"select * from flat.file('{Escape(MissingPath())}') f" + (usePattern ? " where f.Line rlike '^text'" : ""));
        var result = compiled.Run(new TypedQueryRunOptions { DataSourceWarnings = warnings });
        Assert.AreEqual(0, result.Count);
        var warning = warnings.Snapshot().Single();
        Assert.AreEqual(DiagnosticCode.MQ5027_ReportedDataSourceWarning, warning.Code);
        Assert.AreEqual("FLATFILE.Unavailable.Fallback", warning.Arguments["providerCode"]);
        StringAssert.Contains(warning.Message, "empty-result fallback");
        Assert.AreEqual(0, new DataSourceWarningCollector().Snapshot().Count);
    }

    [TestMethod]
    public void DirectoryMissingInput_RemainsEmptyAndReportsWarning()
    {
        var warnings = new DataSourceWarningCollector();
        using var compiled = Compile($"select * from os.directories('{Escape(MissingPath())}', true) d");
        Assert.AreEqual(0, compiled.Run(new TypedQueryRunOptions { DataSourceWarnings = warnings }).Count);
        Assert.AreEqual(DiagnosticCode.MQ5027_ReportedDataSourceWarning, warnings.Snapshot().Single().Code);
    }

    [TestMethod]
    public void InvalidTimeInput_IdentifiesArgumentWithoutEchoingInput()
    {
        var envelope = Failure("time.interval('PRIVATE-INPUT', '2026-01-01', 'days')");
        Assert.AreEqual("TIME.InvalidArgument", envelope.Arguments["providerCode"]);
        Assert.AreEqual("startDateTime", envelope.Arguments["argumentName"]);
        Assert.AreEqual("0", envelope.Arguments["argumentIndex"]);
        Assert.IsFalse(MusoqErrorEnvelopeFormatter.FormatJson(envelope).Contains("PRIVATE-INPUT", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DirectRangeInput_ReportsInvalidIntegralArgument()
    {
        var error = Assert.ThrowsException<DataSourceException>(() => ReadBadRange(SourceArguments.Create("PRIVATE-INPUT", SourceArguments.Empty)));
        Assert.AreEqual("SYSTEM.InvalidRangeArgument", error.Report.Code);
        Assert.AreEqual(0, error.Report.ArgumentIndex);
        Assert.IsFalse(error.Report.Message.Contains("PRIVATE-INPUT", StringComparison.Ordinal));
    }

    [DataTestMethod]
    [DataRow("github", "repositories", "GITHUB_TOKEN")]
    [DataRow("jira", "projects", "JIRA_URL")]
    [DataRow("openai", "gpt", "OPENAI_API_KEY")]
    public void MissingConfiguration_IsReportedBeforeClientAccess(string provider, string method, string setting)
    {
        var schema = RemoteSchema(provider);
        var error = Assert.ThrowsException<DataSourceException>(() =>
            schema.GetRowSource<object, EmptySourceArguments>(method, RuntimeV2TestContexts.CreateExecutionContext(), SourceArguments.Empty));
        Assert.AreEqual(DataSourceFailureKind.InvalidConfiguration, error.Report.FailureKind);
        Assert.AreEqual(setting, error.Report.Details["settingName"]);
        Assert.IsFalse(error.Report.CanRetry);
    }

    [TestMethod]
    public void InvalidOllamaEndpoint_IsReportedBeforeClientAccess()
    {
        var context = RuntimeV2TestContexts.CreateExecutionContext(sourceRuntimeSettings:
            new Dictionary<string, string> { ["OLLAMA_BASE_URL"] = "PRIVATE-INVALID-ENDPOINT" });
        var error = Assert.ThrowsException<DataSourceException>(() =>
            RemoteSchema("ollama").GetRowSource<object, EmptySourceArguments>("llm", context, SourceArguments.Empty));
        Assert.AreEqual("OLLAMA.InvalidConfiguration", error.Report.Code);
        Assert.AreEqual("OLLAMA_BASE_URL", error.Report.Details["settingName"]);
        Assert.IsFalse(error.Report.Message.Contains("PRIVATE", StringComparison.Ordinal));
    }

    private static SchemaBase RemoteSchema(string provider) => provider switch
    {
        "github" => new GitHub.GitHubSchema(), "jira" => new Jira.JiraSchema(),
        "openai" => new OpenAI.OpenAiSchema(), "ollama" => new Ollama.OllamaSchema(),
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    private static void ReadBadRange<TArguments>(TArguments arguments) where TArguments : struct, ISourceArguments
        => new SystemSchema().GetRowSource<object, TArguments>("range", RuntimeV2TestContexts.CreateExecutionContext(), arguments);

    private static MusoqErrorEnvelope Failure(string source)
    {
        try
        {
            using var compiled = Compile("select * from " + source + " s");
            _ = compiled.Run().Count;
            throw new AssertFailedException("Expected a recognized datasource failure.");
        }
        catch (MusoqQueryException error) { return error.PrimaryEnvelope; }
        catch (QueryExecutionException error) { return error.Envelope ?? MusoqErrorEnvelope.FromException(error); }
        catch (Exception error) when (error is not AssertFailedException) { return MusoqErrorEnvelope.FromException(error); }
    }

    private static CompiledQuery Compile(string query)
        => InstanceCreatorHelpers.CompileForExecution(query, Guid.NewGuid().ToString("N"), new Providers(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());

    private static string MissingPath() => Path.Combine(Path.GetTempPath(), "PRIVATE-MISSING-" + Guid.NewGuid().ToString("N"));
    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "''", StringComparison.Ordinal);

    private sealed class Providers : ISchemaProvider
    {
        public ISchema GetSchema(string schema) => schema.ToLowerInvariant() switch
        {
            "#flat" => new FlatFileSchema(),
            "#can" => new CANBusSchema(),
            _ => new RepresentativeSchemaProvider().GetSchema(schema)
        };
    }
}

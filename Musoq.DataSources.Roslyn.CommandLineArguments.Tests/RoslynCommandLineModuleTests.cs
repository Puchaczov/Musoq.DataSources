using System.Net;
using System.Text;
using System.Text.Json;
using Musoq.CommandLine;

namespace Musoq.DataSources.Roslyn.CommandLineArguments.Tests;

[TestClass]
public sealed class RoslynCommandLineModuleTests
{
    private static readonly Func<HttpRequestMessage, CancellationToken,
        ValueTask<(int ExitCode, HttpResponseMessage Response)>> SuccessfulMutation = (_, _) =>
        ValueTask.FromResult((0, Response("{}")));

    public static IEnumerable<object[]> RequestCases
    {
        get
        {
            yield return Case(
                ["csharp", "solution", "load", "repo.sln", "work", "--cache-directory-path", "cache"],
                "bucket/load/work",
                ["solution", "load", "--solution-file-path", "repo.sln", "--cache-directory-path", "cache"]);
            yield return Case(
                ["csharp", "solution", "load", "repo.sln", "work"],
                "bucket/load/work",
                ["solution", "load", "--solution-file-path", "repo.sln", "--cache-directory-path", null]);
            yield return Case(
                ["csharp", "solution", "unload", "repo.sln", "work"],
                "bucket/unload/work",
                ["solution", "unload", "--solution-file-path", "repo.sln"]);
            yield return Case(
                ["csharp", "solution", "cache", "clear", "work", "--cache-directory-path", "cache"],
                "bucket/set/work",
                ["solution", "cache", "clear", "--cache-directory-path", "cache"]);
            yield return Case(
                ["csharp", "solution", "cache", "get", "work"],
                "bucket/get/work",
                ["solution", "cache", "get"]);
            yield return Case(
                ["csharp", "solution", "cache", "set", "work", "--cache-directory-path", "cache"],
                "bucket/set/work",
                ["solution", "cache", "set", "--cache-directory-path", "cache"]);
            yield return Case(
                ["csharp", "solution", "resolve-value-strategy", "get", "work"],
                "bucket/get/work",
                ["solution", "resolve", "value", "strategy", "get"]);
            yield return Case(
                ["csharp", "solution", "resolve-value-strategy", "set", "work", "--value", "UseCustomApiOnly"],
                "bucket/set/work",
                ["solution", "resolve", "value", "strategy", "set", "--value", "UseCustomApiOnly"]);
            yield return Case(
                ["csharp", "solution", "status", "work"],
                "bucket/get/work",
                ["solution", "status"]);
        }
    }

    [TestMethod]
    public void ModuleIsParameterlessAndContributesTheCompleteTree()
    {
        var constructor = typeof(RoslynCommandLineModule).GetConstructor(Type.EmptyTypes);
        Assert.IsNotNull(constructor);

        var application = CreateApplication();
        Assert.HasCount(1, application.Schema.Modules);
        Assert.AreEqual("musoq.datasource.roslyn", application.Schema.Modules.Single().Id);
        Assert.AreEqual("2.0.0", application.Schema.Modules.Single().Version);

        var solution = application.Schema.Root.Children.Single().Children.Single();
        CollectionAssert.AreEqual(
            new[] { "load", "unload", "cache", "resolve-value-strategy", "status" },
            solution.Children.Select(command => command.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "clear", "get", "set" },
            solution.Children.Single(command => command.Name == "cache").Children.Select(command => command.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "get", "set" },
            solution.Children.Single(command => command.Name == "resolve-value-strategy").Children
                .Select(command => command.Name).ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(RequestCases))]
    public async Task CommandsPreserveHttpRequestMappings(
        string[] arguments,
        string expectedUri,
        string?[] expectedArguments)
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedJson = null;
        var callbackToken = default(CancellationToken);
        Func<HttpRequestMessage, CancellationToken, ValueTask<(int ExitCode, HttpResponseMessage Response)>> callback =
            async (request, cancellationToken) =>
            {
                capturedRequest = request;
                callbackToken = cancellationToken;
                capturedJson = await request.Content!.ReadAsStringAsync(cancellationToken);
                return (37, Response("{\"value\":\"result\"}"));
            };
        using var cancellation = new CancellationTokenSource();

        var result = await InvokeAsync(arguments, callback, cancellation.Token);

        Assert.AreEqual(37, result);
        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Post, capturedRequest.Method);
        Assert.AreEqual(expectedUri, capturedRequest.RequestUri!.OriginalString);
        Assert.AreEqual(cancellation.Token, callbackToken);
        Assert.AreEqual("application/json", capturedRequest.Content!.Headers.ContentType!.MediaType);
        using var document = JsonDocument.Parse(capturedJson!);
        Assert.AreEqual("csharp", document.RootElement.GetProperty("schemaName").GetString());
        CollectionAssert.AreEqual(
            expectedArguments,
            document.RootElement.GetProperty("arguments").EnumerateArray()
                .Select(value => value.ValueKind == JsonValueKind.Null ? null : value.GetString())
                .ToArray());
    }

    [TestMethod]
    public async Task StrategyValidationPreventsTransportInvocation()
    {
        var calls = 0;
        var callback = new Func<HttpRequestMessage, CancellationToken,
            ValueTask<(int ExitCode, HttpResponseMessage Response)>>((_, _) =>
        {
            calls++;
            return ValueTask.FromResult((0, Response("{}")));
        });
        var application = CreateApplication();
        var parse = await application.ParseAsync(application.Route(
            "csharp", "solution", "resolve-value-strategy", "set", "work", "--value", "invalid"));
        var validation = await application.ValidateAsync(parse);

        Assert.IsFalse(validation.IsSuccess);
        Assert.HasCount(1, validation.Errors);
        Assert.AreEqual("invalid-resolve-value-strategy", validation.Errors.Single().Code);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public async Task StrategyAcceptsLegacyCaseInsensitiveValues()
    {
        var result = await InvokeAsync(
            ["csharp", "solution", "resolve-value-strategy", "set", "work", "--value", "usecustomapionly"],
            SuccessfulMutation);

        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public async Task CancellationFlowsToTransport()
    {
        using var cancellation = new CancellationTokenSource();
        var callback = new Func<HttpRequestMessage, CancellationToken,
            ValueTask<(int ExitCode, HttpResponseMessage Response)>>((_, cancellationToken) =>
        {
            Assert.AreEqual(cancellation.Token, cancellationToken);
            throw new OperationCanceledException(cancellationToken);
        });

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await InvokeAsync(
                ["csharp", "solution", "cache", "get", "work"],
                callback,
                cancellation.Token));
    }

    [TestMethod]
    public async Task MissingTransportItemFailsAtInvocationOnly()
    {
        var application = CreateApplication();
        var parse = await application.ParseAsync(application.Route("csharp", "solution", "cache", "get", "work"));
        var validation = await application.ValidateAsync(parse);
        Assert.IsTrue(validation.IsSuccess);

        var exception = await Assert.ThrowsExactlyAsync<KeyNotFoundException>(async () =>
            await application.InvokeAsync(validation.Invocation!));

        StringAssert.Contains(exception.Message, "musoq.datasource.http-request.v2");
    }

    [TestMethod]
    public async Task ValueCommandsWriteReturnedValueAndMutationsStaySilent()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var result = await InvokeAsync(
            ["csharp", "solution", "resolve-value-strategy", "get", "work"],
            (_, _) => ValueTask.FromResult((0, Response("{\"value\":\"UseCustomApiOnly\"}"))),
            output: output,
            error: error);

        Assert.AreEqual(0, result);
        Assert.AreEqual("UseCustomApiOnly" + Environment.NewLine, output.ToString());
        Assert.AreEqual(string.Empty, error.ToString());

        output.GetStringBuilder().Clear();
        var mutation = await InvokeAsync(
            ["csharp", "solution", "cache", "set", "work", "--cache-directory-path", "cache"],
            SuccessfulMutation,
            output: output,
            error: error);
        Assert.AreEqual(0, mutation);
        Assert.AreEqual(string.Empty, output.ToString());
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task StatusWritesDeterministicBucketPrefixedValue()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var result = await InvokeAsync(
            ["csharp", "solution", "status", "work"],
            (_, _) => ValueTask.FromResult((0, Response(JsonSerializer.Serialize(new
            {
                value = string.Join(Environment.NewLine,
                    "Loaded solutions: 0",
                    "Cache directory: C:\\cache",
                    "Resolve value strategy: UseNugetOrgApiOnly")
            })))),
            output: output,
            error: error);

        Assert.AreEqual(0, result);
        Assert.AreEqual(
            $"Bucket: work{Environment.NewLine}Loaded solutions: 0{Environment.NewLine}Cache directory: C:\\cache{Environment.NewLine}Resolve value strategy: UseNugetOrgApiOnly{Environment.NewLine}",
            output.ToString());
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task HttpFailureAndMalformedOrNullSuccessUseStandardError()
    {
        var failureOutput = new StringWriter();
        var failureError = new StringWriter();
        var failure = await InvokeAsync(
            ["csharp", "solution", "cache", "get", "work"],
            (_, _) => ValueTask.FromResult((23, new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"message\":\"bucket missing\"}")
            })),
            output: failureOutput,
            error: failureError);

        Assert.AreEqual(23, failure);
        Assert.AreEqual(string.Empty, failureOutput.ToString());
        Assert.AreEqual("bucket missing" + Environment.NewLine, failureError.ToString());

        foreach (var body in new[] { "not-json", "{\"result\":\"ok\",\"value\":null}" })
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var response = Response(body);
            var malformed = await InvokeAsync(
                ["csharp", "solution", "cache", "get", "work"],
                (_, _) => ValueTask.FromResult((0, response)),
                output: output,
                error: error);

            Assert.AreEqual(1, malformed);
            Assert.AreEqual(string.Empty, output.ToString());
            StringAssert.Contains(error.ToString(), "Datasource protocol error:");
            await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () => await response.Content.ReadAsStringAsync());
        }
    }

    [TestMethod]
    public async Task ResponseIsDisposedAfterSuccessAndFailure()
    {
        var success = Response("{\"value\":\"value\"}");
        await InvokeAsync(
            ["csharp", "solution", "cache", "get", "work"],
            (_, _) => ValueTask.FromResult((0, success)));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () => await success.Content.ReadAsStringAsync());

        var failure = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"message\":\"failed\"}")
        };
        await InvokeAsync(
            ["csharp", "solution", "cache", "get", "work"],
            (_, _) => ValueTask.FromResult((9, failure)));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () => await failure.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public void SymbolsExposePathAndStrategyCompletionHints()
    {
        var application = CreateApplication();
        var solution = application.Schema.Root.Children.Single().Children.Single();
        var load = solution.Children.Single(command => command.Name == "load");
        var strategy = solution.Children.Single(command => command.Name == "resolve-value-strategy")
            .Children.Single(command => command.Name == "set");

        Assert.AreEqual(
            Musoq.CommandLine.Completion.CompletionDirective.FileCompletion,
            load.Symbols.Single(symbol => symbol.Name == "path").CompletionDirective);
        Assert.AreEqual(
            Musoq.CommandLine.Completion.CompletionDirective.DirectoryCompletion,
            load.Symbols.Single(symbol => symbol.Name == "--cache-directory-path").CompletionDirective);
        Assert.AreEqual(1, strategy.Symbols.Single(symbol => symbol.Name == "--value").CompletionProviders.Count);
    }

    private static CommandLineApplication CreateApplication()
    {
        var builder = CommandLineApplication.CreateBuilder("musoq");
        builder.AddModule(new RoslynCommandLineModule());
        return builder.Build();
    }

    private static async Task<int> InvokeAsync(
        string[] arguments,
        Func<HttpRequestMessage, CancellationToken, ValueTask<(int ExitCode, HttpResponseMessage Response)>> callback,
        CancellationToken cancellationToken = default,
        StringWriter? output = null,
        StringWriter? error = null)
    {
        var application = CreateApplication();
        var parse = await application.ParseAsync(application.Route(arguments), cancellationToken: cancellationToken);
        Assert.IsTrue(
            parse.IsSuccess,
            string.Join(Environment.NewLine, parse.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        var validation = await application.ValidateAsync(parse, cancellationToken: cancellationToken);
        Assert.IsTrue(
            validation.IsSuccess,
            string.Join(Environment.NewLine, validation.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        return await application.InvokeAsync(
            validation.Invocation!,
            new CommandLineInvocationContext(
                StandardOutput: output ?? new StringWriter(),
                StandardError: error ?? new StringWriter(),
                Items: new Dictionary<object, object?>
                {
                    [RoslynCommandLineModule.HttpRequestV2] = callback
                }),
            cancellationToken);
    }

    private static HttpResponseMessage Response(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static object[] Case(string[] arguments, string uri, string?[] payloadArguments) =>
        [arguments, uri, payloadArguments];
}

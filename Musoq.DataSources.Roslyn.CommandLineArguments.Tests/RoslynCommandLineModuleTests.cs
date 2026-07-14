using System.Net;
using System.Text.Json;
using Musoq.CommandLine;

namespace Musoq.DataSources.Roslyn.CommandLineArguments.Tests;

[TestClass]
public sealed class RoslynCommandLineModuleTests
{
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

        Assert.HasCount(1, application.Schema.Root.Children);
        var csharp = application.Schema.Root.Children.Single();
        Assert.HasCount(1, csharp.Children);
        var solution = csharp.Children.Single();
        CollectionAssert.AreEqual(
            new[] { "load", "unload", "cache", "resolve-value-strategy" },
            solution.Children.Select(command => command.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "clear", "get", "set" },
            solution.Children.Single(command => command.Name == "cache").Children.Select(command => command.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "get", "set" },
            solution.Children.Single(command => command.Name == "resolve-value-strategy").Children.Select(command => command.Name).ToArray());
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
        Func<HttpRequestMessage, CancellationToken, ValueTask<int>> callback = async (request, cancellationToken) =>
        {
            capturedRequest = request;
            callbackToken = cancellationToken;
            capturedJson = await request.Content!.ReadAsStringAsync(cancellationToken);
            return 37;
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
        Func<HttpRequestMessage, CancellationToken, ValueTask<int>> callback = (_, _) =>
        {
            calls++;
            return ValueTask.FromResult(0);
        };
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
        Func<HttpRequestMessage, CancellationToken, ValueTask<int>> callback = (_, _) => ValueTask.FromResult(0);

        var result = await InvokeAsync(
            ["csharp", "solution", "resolve-value-strategy", "set", "work", "--value", "usecustomapionly"],
            callback);

        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public async Task CancellationFlowsToTransport()
    {
        using var cancellation = new CancellationTokenSource();
        Func<HttpRequestMessage, CancellationToken, ValueTask<int>> callback = (_, cancellationToken) =>
        {
            Assert.AreEqual(cancellation.Token, cancellationToken);
            throw new OperationCanceledException(cancellationToken);
        };

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

        StringAssert.Contains(exception.Message, "musoq.datasource.http-request.v1");
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
        Func<HttpRequestMessage, CancellationToken, ValueTask<int>> callback,
        CancellationToken cancellationToken = default)
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
                Items: new Dictionary<object, object?>
                {
                    [RoslynCommandLineModule.HttpRequestItem] = callback
                }),
            cancellationToken);
    }

    private static object[] Case(string[] arguments, string uri, string?[] payloadArguments) =>
        [arguments, uri, payloadArguments];
}

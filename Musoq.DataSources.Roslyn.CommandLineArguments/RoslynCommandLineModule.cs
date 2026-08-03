using System.Net.Http.Json;
using System.Text.Json;
using Musoq.CommandLine;
using Musoq.DataSources.Roslyn.CommandLineArguments.Dtos;

namespace Musoq.DataSources.Roslyn.CommandLineArguments;

public sealed class RoslynCommandLineModule : ICommandModule
{
    private static readonly string[] ResolveValueStrategies =
    [
        "UseNugetOrgApiOnly",
        "UseCustomApiOnly",
        "UseNugetOrgApiAndCustomApi"
    ];

    public static CommandLineItemKey<
        Func<HttpRequestMessage, CancellationToken, ValueTask<(int ExitCode, HttpResponseMessage Response)>>> HttpRequestV2 { get; } =
        new("musoq.datasource.http-request.v2");

    public CommandModuleDescriptor Descriptor { get; } =
        new("musoq.datasource.roslyn", "3.0.0");

    public void Configure(CommandModuleBuilder module)
    {
        module.Contribute(CommandMountId.Root, root =>
            root.Command("csharp", csharp =>
            {
                csharp.Description("C# solution management.");
                csharp.NonExecutable();
                csharp.Command("solution", solution =>
                {
                    solution.Description("Manage loaded C# solutions.");
                    solution.NonExecutable();
                    ConfigureLoad(solution);
                    ConfigureUnload(solution);
                    ConfigureCache(solution);
                    ConfigureResolveValueStrategy(solution);
                    ConfigureStatus(solution);
                });
            }));
    }

    private static void ConfigureLoad(CommandBuilder solution)
    {
        solution.Command("load", command =>
        {
            command.Description("Loads solution to memory");
            var bucket = command.Argument<string>("bucket")
                .Description("Bucket identifier");
            var path = command.Argument<string>("path")
                .Description("Path to the solution file")
                .CompleteFiles();
            var cacheDirectoryPath = command.Option<string?>("--cache-directory-path", null)
                .Description("Optional cache directory path")
                .CompleteDirectories();

            command.HandleWithContext((context, cancellationToken) =>
                LoadSolutionAsync(
                    context,
                    cancellationToken,
                    bucket.Get(context.Values),
                    path.Get(context.Values),
                    cacheDirectoryPath.Get(context.Values)));
        });
    }

    private static async ValueTask<int> LoadSolutionAsync(
        CommandExecutionContext context,
        CancellationToken cancellationToken,
        string bucket,
        string path,
        string? cacheDirectoryPath)
    {
        var createExitCode = await InvokeAsync(
                context,
                cancellationToken,
                new HttpRequestMessage(HttpMethod.Post, $"bucket/create/{bucket}"))
            .ConfigureAwait(false);
        if (createExitCode != 0)
            return createExitCode;

        return await InvokeAsync(
                context,
                cancellationToken,
                $"bucket/load/{bucket}",
                new LoadBucketRequestDto
                {
                    SchemaName = "csharp",
                    Arguments =
                    [
                        "solution",
                        "load",
                        "--solution-file-path",
                        path,
                        "--cache-directory-path",
                        cacheDirectoryPath
                    ]
                })
            .ConfigureAwait(false);
    }

    private static void ConfigureUnload(CommandBuilder solution)
    {
        solution.Command("unload", command =>
        {
            command.Description("Unload solution from memory");
            var path = command.Argument<string>("path")
                .Description("Path to the solution file")
                .CompleteFiles();
            var bucket = command.Argument<string>("bucket")
                .Description("Bucket identifier");

            command.HandleWithContext((context, cancellationToken) =>
                InvokeAsync(
                    context,
                    cancellationToken,
                    $"bucket/unload/{bucket.Get(context.Values)}",
                    new UnloadBucketRequestDto
                    {
                        SchemaName = "csharp",
                        Arguments =
                        [
                            "solution",
                            "unload",
                            "--solution-file-path",
                            path.Get(context.Values)
                        ]
                    }));
        });
    }

    private static void ConfigureCache(CommandBuilder solution)
    {
        solution.Command("cache", cache =>
        {
            cache.Description("Manage the Roslyn solution cache.");
            cache.NonExecutable();

            cache.Command("clear", command => ConfigureCacheMutation(command, "Clears cache directory", "clear"));
            cache.Command("get", command =>
            {
                command.Description("Gets cache directory path");
                var bucket = command.Argument<string>("bucket")
                    .Description("Bucket identifier");

                command.HandleWithContext((context, cancellationToken) =>
                    InvokeAsync(
                        context,
                        cancellationToken,
                        $"bucket/get/{bucket.Get(context.Values)}",
                        new GetBucketRequestDto
                        {
                            SchemaName = "csharp",
                            Arguments = ["solution", "cache", "get"]
                        },
                        writeValue: true));
            });
            cache.Command("set", command => ConfigureCacheMutation(command, "Sets cache directory path", "set"));
        });
    }

    private static void ConfigureCacheMutation(CommandBuilder command, string description, string operation)
    {
        command.Description(description);
        var bucket = command.Argument<string>("bucket")
            .Description("Bucket identifier");
        var cacheDirectoryPath = command.Option<string?>("--cache-directory-path", null)
            .Description("Optional cache directory path")
            .CompleteDirectories();

        command.HandleWithContext((context, cancellationToken) =>
            InvokeAsync(
                context,
                cancellationToken,
                $"bucket/set/{bucket.Get(context.Values)}",
                new SetBucketRequestDto
                {
                    SchemaName = "csharp",
                    Arguments =
                    [
                        "solution",
                        "cache",
                        operation,
                        "--cache-directory-path",
                        cacheDirectoryPath.Get(context.Values)
                    ]
                }));
    }

    private static void ConfigureResolveValueStrategy(CommandBuilder solution)
    {
        solution.Command("resolve-value-strategy", resolveValueStrategy =>
        {
            resolveValueStrategy.Description("Manage NuGet value resolution strategy.");
            resolveValueStrategy.NonExecutable();

            resolveValueStrategy.Command("get", command =>
            {
                command.Description(
                    "Gets resolve value strategy. Will be equal to UseNugetOrgApiOnly | UseCustomApiOnly | UseNugetOrgApiAndCustomApi");
                var bucket = command.Argument<string>("bucket")
                    .Description("Bucket identifier");

                command.HandleWithContext((context, cancellationToken) =>
                    InvokeAsync(
                        context,
                        cancellationToken,
                        $"bucket/get/{bucket.Get(context.Values)}",
                        new GetBucketRequestDto
                        {
                            SchemaName = "csharp",
                            Arguments = ["solution", "resolve", "value", "strategy", "get"]
                        },
                        writeValue: true));
            });

            resolveValueStrategy.Command("set", command =>
            {
                command.Description(
                    "Sets resolve value strategy. Must be equal to UseNugetOrgApiOnly | UseCustomApiOnly | UseNugetOrgApiAndCustomApi");
                var bucket = command.Argument<string>("bucket")
                    .Description("Bucket identifier");
                var strategy = command.RequiredOption<string>("--value")
                    .Description(
                        "Resolve value strategy: UseNugetOrgApiOnly | UseCustomApiOnly | UseNugetOrgApiAndCustomApi")
                    .CompleteWith(ResolveValueStrategies)
                    .Validate(
                        value => ResolveValueStrategies.Contains(value, StringComparer.OrdinalIgnoreCase)
                            ? null
                            : $"Resolve value strategy must be one of: {string.Join(", ", ResolveValueStrategies)}.",
                        "invalid-resolve-value-strategy");

                command.HandleWithContext((context, cancellationToken) =>
                    InvokeAsync(
                        context,
                        cancellationToken,
                        $"bucket/set/{bucket.Get(context.Values)}",
                        new SetBucketRequestDto
                        {
                            SchemaName = "csharp",
                            Arguments =
                            [
                                "solution",
                                "resolve",
                                "value",
                                "strategy",
                                "set",
                                "--value",
                                strategy.Get(context.Values)
                            ]
                        }));
            });
        });
    }

    private static void ConfigureStatus(CommandBuilder solution)
    {
        solution.Command("status", command =>
        {
            command.Description("Prints the loaded solution status.");
            var bucket = command.Argument<string>("bucket")
                .Description("Bucket identifier");

            command.HandleWithContext((context, cancellationToken) =>
                InvokeAsync(
                    context,
                    cancellationToken,
                    $"bucket/get/{bucket.Get(context.Values)}",
                    new GetBucketRequestDto
                    {
                        SchemaName = "csharp",
                        Arguments = ["solution", "status"]
                    },
                    writeValue: true,
                    formatStatus: value => $"Bucket: {bucket.Get(context.Values)}{Environment.NewLine}{value}"));
        });
    }

    private static async ValueTask<int> InvokeAsync<TRequest>(
        CommandExecutionContext context,
        CancellationToken cancellationToken,
        string requestUri,
        TRequest payload,
        bool writeValue = false,
        Func<string, string>? formatStatus = null)
    {
        return await InvokeAsync(
                context,
                cancellationToken,
                new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = JsonContent.Create(payload)
                },
                writeValue,
                formatStatus)
            .ConfigureAwait(false);
    }

    private static async ValueTask<int> InvokeAsync(
        CommandExecutionContext context,
        CancellationToken cancellationToken,
        HttpRequestMessage request,
        bool writeValue = false,
        Func<string, string>? formatStatus = null)
    {
        using (request)
        {
            var invokeAsync = context.GetRequiredItem(HttpRequestV2);
            var (exitCode, response) = await invokeAsync(request, cancellationToken).ConfigureAwait(false);
            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    await context.StandardError.WriteLineAsync(
                        ExtractErrorMessage(body, response).AsMemory(),
                        cancellationToken).ConfigureAwait(false);
                    return exitCode;
                }

                if (!writeValue)
                    return exitCode;

                if (!TryReadValue(body, out var value, out var protocolError))
                {
                    await context.StandardError.WriteLineAsync(protocolError.AsMemory(), cancellationToken)
                        .ConfigureAwait(false);
                    return 1;
                }

                value = formatStatus is null ? value : formatStatus(value);
                await context.StandardOutput.WriteLineAsync(value.AsMemory(), cancellationToken).ConfigureAwait(false);
                return exitCode;
            }
        }
    }

    private static bool TryReadValue(string body, out string value, out string error)
    {
        value = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("value", out var valueElement) ||
                valueElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                error = "Datasource protocol error: successful AgentLocal response is missing a value.";
                return false;
            }

            value = valueElement.ValueKind == JsonValueKind.String
                ? valueElement.GetString() ?? string.Empty
                : valueElement.GetRawText();
            if (value.Length == 0)
            {
                error = "Datasource protocol error: successful AgentLocal response is missing a value.";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (JsonException)
        {
            error = "Datasource protocol error: successful AgentLocal response was not valid JSON.";
            return false;
        }
    }

    private static string ExtractErrorMessage(string body, HttpResponseMessage response)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(message.GetString()))
                return message.GetString()!;
        }
        catch (JsonException)
        {
        }

        return !string.IsNullOrWhiteSpace(response.ReasonPhrase)
            ? response.ReasonPhrase!
            : "AgentLocal request failed.";
    }
}

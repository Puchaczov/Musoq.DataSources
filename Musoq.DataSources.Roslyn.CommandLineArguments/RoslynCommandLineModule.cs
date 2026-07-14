using System.Net.Http.Json;
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

    public static CommandLineItemKey<Func<HttpRequestMessage, CancellationToken, ValueTask<int>>> HttpRequestItem { get; } =
        new("musoq.datasource.http-request.v1");

    public CommandModuleDescriptor Descriptor { get; } =
        new("musoq.datasource.roslyn", "1.0.0");

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
                });
            }));
    }

    private static void ConfigureLoad(CommandBuilder solution)
    {
        solution.Command("load", command =>
        {
            command.Description("Loads solution to memory");
            var path = command.Argument<string>("path")
                .Description("Path to the solution file")
                .CompleteFiles();
            var bucket = command.Argument<string>("bucket")
                .Description("Bucket identifier");
            var cacheDirectoryPath = command.Option<string?>("--cache-directory-path", null)
                .Description("Optional cache directory path")
                .CompleteDirectories();

            command.HandleWithContext((context, cancellationToken) =>
                InvokeAsync(
                    context,
                    cancellationToken,
                    $"bucket/load/{bucket.Get(context.Values)}",
                    new LoadBucketRequestDto
                    {
                        SchemaName = "csharp",
                        Arguments =
                        [
                            "solution",
                            "load",
                            "--solution-file-path",
                            path.Get(context.Values),
                            "--cache-directory-path",
                            cacheDirectoryPath.Get(context.Values)
                        ]
                    }));
        });
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
                        }));
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
                        }));
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

    private static async ValueTask<int> InvokeAsync<TRequest>(
        CommandExecutionContext context,
        CancellationToken cancellationToken,
        string requestUri,
        TRequest payload)
    {
        var invokeAsync = context.GetRequiredItem(HttpRequestItem);
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(payload)
        };
        return await invokeAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

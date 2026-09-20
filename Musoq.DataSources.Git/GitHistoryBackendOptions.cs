using System;
using System.Collections.Generic;

namespace Musoq.DataSources.Git;

internal enum GitHistoryBackend
{
    Auto,
    GitCli,
    LibGit2
}

/// <summary>
/// Resolves the per-execution history backend configuration. Values are deliberately not read from the host process
/// environment: Musoq's runtime settings are the documented and auditable configuration boundary.
/// </summary>
internal sealed class GitHistoryBackendOptions
{
    public const string BackendSettingName = "GIT_HISTORY_BACKEND";
    public const string ExecutableSettingName = "GIT_EXECUTABLE";

    private GitHistoryBackendOptions(GitHistoryBackend backend, string executable)
    {
        Backend = backend;
        Executable = executable;
    }

    public GitHistoryBackend Backend { get; }

    public string Executable { get; }

    /// <summary>Read-only CLI defaults used by internal operation readers that have no public backend setting.</summary>
    public static GitHistoryBackendOptions Default { get; } = new(GitHistoryBackend.Auto, "git");

    public static GitHistoryBackendOptions From(IReadOnlyDictionary<string, string> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var backendText = settings.TryGetValue(BackendSettingName, out var configuredBackend)
            ? configuredBackend
            : "auto";
        var backend = backendText.Trim().ToLowerInvariant() switch
        {
            "auto" => GitHistoryBackend.Auto,
            "git-cli" => GitHistoryBackend.GitCli,
            "libgit2" => GitHistoryBackend.LibGit2,
            _ => throw new InvalidOperationException(
                $"Runtime setting '{BackendSettingName}' must be one of: auto, git-cli, libgit2. " +
                $"The supplied value was '{backendText}'.")
        };

        var executable = settings.TryGetValue(ExecutableSettingName, out var configuredExecutable) &&
                         !string.IsNullOrWhiteSpace(configuredExecutable)
            ? configuredExecutable.Trim()
            : "git";

        return new GitHistoryBackendOptions(backend, executable);
    }
}

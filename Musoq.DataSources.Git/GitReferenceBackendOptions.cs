using System;
using System.Collections.Generic;

namespace Musoq.DataSources.Git;

/// <summary>Resolves backend settings for local and remote Git reference readers.</summary>
internal sealed class GitReferenceBackendOptions
{
    public const string BackendSettingName = "GIT_REFERENCE_BACKEND";
    public const string ExecutableSettingName = GitHistoryBackendOptions.ExecutableSettingName;

    private GitReferenceBackendOptions(GitHistoryBackend backend, string executable)
    {
        Backend = backend;
        Executable = executable;
    }

    public GitHistoryBackend Backend { get; }

    public string Executable { get; }

    public static GitReferenceBackendOptions Default { get; } = new(GitHistoryBackend.Auto, "git");

    public static GitReferenceBackendOptions From(IReadOnlyDictionary<string, string> settings)
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

        return new GitReferenceBackendOptions(backend, executable);
    }
}

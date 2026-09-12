using System.Diagnostics;

namespace Musoq.DataSources.Git.Tests;

/// <summary>
/// Creates independent local working and bare repositories for offline Git integration tests.
/// </summary>
internal sealed class OfflineGitFixture : IDisposable
{
    private OfflineGitFixture(
        string root,
        string seedPath,
        string clientPath,
        string remotePath,
        string backupRemotePath)
    {
        Root = root;
        SeedPath = seedPath;
        ClientPath = clientPath;
        RemotePath = remotePath;
        BackupRemotePath = backupRemotePath;
    }

    public string Root { get; }

    public string SeedPath { get; }

    public string ClientPath { get; }

    public string RemotePath { get; }

    public string BackupRemotePath { get; }

    public string RemoteUrl => new Uri(RemotePath).AbsoluteUri;

    public string BackupRemoteUrl => new Uri(BackupRemotePath).AbsoluteUri;

    public static OfflineGitFixture Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "musoq-git-offline-" + Guid.NewGuid().ToString("N"));
        var seedPath = Path.Combine(root, "seed");
        var clientPath = Path.Combine(root, "client");
        var remotePath = Path.Combine(root, "remote.git");
        var backupRemotePath = Path.Combine(root, "backup.git");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(seedPath);
        Directory.CreateDirectory(clientPath);

        RunGit(seedPath, "init", "-q");
        ConfigureIdentity(seedPath);
        RunGit(seedPath, "commit", "--allow-empty", "-q", "-m", "seed");
        RunGit(seedPath, "branch", "-M", "main");

        RunGit(root, "init", "--bare", "-q", remotePath);
        RunGit(root, "init", "--bare", "-q", backupRemotePath);
        AddLocalRemote(seedPath, "origin", remotePath);
        AddLocalRemote(seedPath, "backup", backupRemotePath);
        RunGit(seedPath, "push", "-q", "origin", "main");
        RunGit(seedPath, "push", "-q", "backup", "main");

        RunGit(clientPath, "init", "-q");
        ConfigureIdentity(clientPath);
        AddLocalRemote(clientPath, "origin", remotePath);
        AddLocalRemote(clientPath, "backup", backupRemotePath);

        return new OfflineGitFixture(root, seedPath, clientPath, remotePath, backupRemotePath);
    }

    public void CreateLightweightTag(string name)
    {
        RunGit(SeedPath, "tag", name);
        RunGit(SeedPath, "push", "-q", "origin", $"refs/tags/{name}");
        RunGit(SeedPath, "push", "-q", "backup", $"refs/tags/{name}");
    }

    public void CreateAnnotatedTag(string name, string message)
    {
        RunGit(SeedPath, "tag", "-a", name, "-m", message);
        RunGit(SeedPath, "push", "-q", "origin", $"refs/tags/{name}");
        RunGit(SeedPath, "push", "-q", "backup", $"refs/tags/{name}");
    }

    public void CreateStash(string message, bool includeUntrackedFiles = false)
    {
        var fileName = "tracked-" + Guid.NewGuid().ToString("N") + ".txt";
        var path = Path.Combine(SeedPath, fileName);
        File.WriteAllText(path, "base");
        RunGit(SeedPath, "add", fileName);
        RunGit(SeedPath, "commit", "--quiet", "-m", "stash base");
        File.WriteAllText(path, "modified");
        var arguments = new List<string> { "stash", "push", "--quiet", "--message", message };
        if (includeUntrackedFiles)
        {
            File.WriteAllText(Path.Combine(SeedPath, "untracked-" + Guid.NewGuid().ToString("N") + ".txt"), "untracked");
            arguments.Insert(2, "--include-untracked");
        }

        RunGit(SeedPath, arguments.ToArray());
    }

    public string RunGit(params string[] arguments) => RunGit(ClientPath, arguments);

    public static string RunGit(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ??
                            throw new InvalidOperationException("Unable to start the local Git executable.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Local Git command failed with exit code {process.ExitCode}: {error.Trim()}");

        return output;
    }

    public void Dispose()
    {
        if (!Directory.Exists(Root))
            return;

        foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);

        Directory.Delete(Root, recursive: true);
    }

    private static void ConfigureIdentity(string path)
    {
        RunGit(path, "config", "user.name", "Musoq Offline Test");
        RunGit(path, "config", "user.email", "musoq-offline@example.invalid");
    }

    private static void AddLocalRemote(string repositoryPath, string name, string remotePath)
    {
        if (!Path.IsPathRooted(remotePath) || !Directory.Exists(remotePath))
            throw new ArgumentException("Offline Git fixtures accept only existing local remote paths.", nameof(remotePath));

        RunGit(repositoryPath, "remote", "add", name, remotePath);
        RunGit(repositoryPath, "remote", "set-url", "--push", name, remotePath);
    }
}

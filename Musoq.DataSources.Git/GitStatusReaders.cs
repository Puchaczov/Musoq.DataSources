using System;
using System.IO;
using System.Threading;
using LibGit2Sharp;

namespace Musoq.DataSources.Git;

internal interface IGitStatusReader
{
    string Backend { get; }

    void Read(string repositoryPath, Func<string, Repository> createRepository, CancellationToken cancellationToken,
        Func<GitStatusRecord, bool> onStatus);
}

internal readonly record struct GitStatusRecord(string FilePath, FileStatus State);

internal sealed class LibGit2StatusReader : IGitStatusReader
{
    public string Backend => "libgit2";

    public void Read(string repositoryPath, Func<string, Repository> createRepository, CancellationToken cancellationToken,
        Func<GitStatusRecord, bool> onStatus)
    {
        using var repository = createRepository(repositoryPath);
        foreach (var entry in repository.RetrieveStatus())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!onStatus(new GitStatusRecord(entry.FilePath, entry.State)))
                break;
        }
    }
}

/// <summary>Safe CLI candidate. It preserves Git's default excluded-ignored behavior and uses NUL paths.</summary>
internal sealed class GitCliStatusReader : IGitStatusReader
{
    public string Backend => "git-cli";

    public void Read(string repositoryPath, Func<string, Repository> createRepository, CancellationToken cancellationToken,
        Func<GitStatusRecord, bool> onStatus)
    {
        using var process = GitCliProcess.Start(
            repositoryPath,
            GitHistoryBackendOptions.Default,
            ["status", "--porcelain=v1", "-z", "--untracked-files=all"],
            cancellationToken);
        using var reader = new GitNulDelimitedUtf8Reader(process.StandardOutput);
        var completedNaturally = true;
        while (reader.ReadToken() is { } entry)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.Length < 3 || entry[2] != ' ')
                throw new InvalidDataException("Git status protocol returned an invalid porcelain record.");
            var index = entry[0];
            var workTree = entry[1];
            var path = entry[3..];
            if (index is 'R' or 'C' || workTree is 'R' or 'C')
                _ = reader.ReadToken() ?? throw new InvalidDataException("Git status rename record omitted its original path.");
            if (!onStatus(new GitStatusRecord(path, ToFileStatus(index, workTree))))
            {
                completedNaturally = false;
                process.Stop();
                break;
            }
        }

        if (completedNaturally)
            process.Complete();
    }

    private static FileStatus ToFileStatus(char index, char workTree)
    {
        if (index == '?' && workTree == '?')
            return FileStatus.NewInWorkdir;
        if (index == '!' && workTree == '!')
            return FileStatus.Ignored;
        if (index == 'U' || workTree == 'U' || (index == 'A' && workTree == 'A') ||
            (index == 'D' && workTree == 'D'))
            return FileStatus.Conflicted;
        return IndexStatus(index) | WorkTreeStatus(workTree);
    }

    private static FileStatus IndexStatus(char value) => value switch
    {
        'A' => FileStatus.NewInIndex,
        'M' => FileStatus.ModifiedInIndex,
        'D' => FileStatus.DeletedFromIndex,
        'R' or 'C' => FileStatus.RenamedInIndex,
        'T' => FileStatus.TypeChangeInIndex,
        _ => FileStatus.Unaltered
    };

    private static FileStatus WorkTreeStatus(char value) => value switch
    {
        'M' => FileStatus.ModifiedInWorkdir,
        'D' => FileStatus.DeletedFromWorkdir,
        'R' or 'C' => FileStatus.RenamedInWorkdir,
        'T' => FileStatus.TypeChangeInWorkdir,
        _ => FileStatus.Unaltered
    };
}

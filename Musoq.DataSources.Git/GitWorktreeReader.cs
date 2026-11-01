using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Musoq.DataSources.Git.Entities;

namespace Musoq.DataSources.Git;

internal sealed class GitWorktreeReader(IGitCliProcessFactory? factory = null)
{
    public IEnumerable<WorktreeEntity> Read(string path, GitHistoryBackendOptions options, CancellationToken token,
        GitRepositoryLocation? location = null)
    {
        token.ThrowIfCancellationRequested();
        using var process = (factory ?? GitCliProcessFactory.Default).Start(new GitCliProcessRequest(path,
            options.Executable, string.Empty, ["worktree", "list", "--porcelain", "-z"], Location: location), token);
        using var reader = new GitNulDelimitedUtf8Reader(process.StandardOutput);
        foreach (var entity in Parse(reader, token)) yield return entity;
        process.Complete();
    }

    internal static IEnumerable<WorktreeEntity> Parse(GitNulDelimitedUtf8Reader reader, CancellationToken token)
    {
        string? path = null, head = null, branch = null, lockReason = null, pruneReason = null;
        bool bare = false, detached = false, locked = false, prunable = false;
        while (reader.ReadToken() is { } field)
        {
            token.ThrowIfCancellationRequested();
            if (field.Length == 0)
            {
                if (path is null) continue;
                yield return new WorktreeEntity(path, head, branch, bare, detached, locked, lockReason, prunable, pruneReason);
                path = head = branch = lockReason = pruneReason = null;
                bare = detached = locked = prunable = false;
                continue;
            }
            var separator = field.IndexOf(' ');
            var label = separator < 0 ? field : field[..separator];
            var value = separator < 0 ? null : field[(separator + 1)..];
            if (label == "worktree")
            {
                if (path is not null || string.IsNullOrEmpty(value))
                    throw new InvalidDataException("Git worktree output has an invalid record boundary.");
                path = value;
                continue;
            }
            if (path is null) throw new InvalidDataException("Git worktree output omitted the worktree path.");
            switch (label)
            {
                case "HEAD":
                    head = value is not null && value.Trim('0').Length > 0 ? value : null;
                    break;
                case "branch": branch = value; break;
                case "bare": bare = true; break;
                case "detached": detached = true; break;
                case "locked": locked = true; lockReason = value; break;
                case "prunable": prunable = true; pruneReason = value; break;
            }
        }
        if (path is not null) throw new InvalidDataException("Git worktree output ended before its record terminator.");
    }
}

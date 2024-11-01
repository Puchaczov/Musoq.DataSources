using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

public class StashEntity(Stash stash)
{
    public string Message => stash.Message;

    public CommitEntity Commit => new CommitEntity(stash.Index);
}
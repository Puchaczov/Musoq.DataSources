using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

public class CommitEntity(Commit commit)
{
    public string Sha => commit.Sha;
    
    public string Message => commit.Message;
    
    public string MessageShort => commit.MessageShort;
    
    public string Author => commit.Author.Name;
    
    public string Committer => commit.Committer.Name;
}
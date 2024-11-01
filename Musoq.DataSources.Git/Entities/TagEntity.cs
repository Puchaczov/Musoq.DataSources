using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

public class TagEntity(Tag tag)
{
    public string? FriendlyName => tag.FriendlyName;
    
    public string? CanonicalName => tag.CanonicalName;
    
    public string? Message => tag.Annotation?.Message;
}
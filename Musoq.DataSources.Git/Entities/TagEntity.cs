using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
/// Represents a tag entity in a Git repository.
/// </summary>
public class TagEntity
{
    private readonly Tag _tag;
    private readonly Repository _libGitRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="TagEntity"/> class.
    /// </summary>
    /// <param name="tag">The tag object from LibGit2Sharp.</param>
    /// <param name="repository">The Git repository.</param>
    public TagEntity(Tag tag, Repository repository)
    {
        _tag = tag;
        _libGitRepository = repository;
    }

    /// <summary>
    /// Gets the friendly name of the tag.
    /// </summary>
    public string? FriendlyName => _tag.FriendlyName;
    
    /// <summary>
    /// Gets the canonical name of the tag.
    /// </summary>
    public string? CanonicalName => _tag.CanonicalName;

    /// <summary>
    /// Gets the message of the tag annotation.
    /// </summary>
    public string? Message => _tag.Annotation?.Message;

    /// <summary>
    /// Gets a value indicating whether the tag is annotated.
    /// </summary>
    public bool IsAnnotated => _tag.IsAnnotated;

    /// <summary>
    /// Gets the annotation entity of the tag.
    /// </summary>
    public AnnotationEntity Annotation => new(_tag.Annotation, _libGitRepository);

    /// <summary>
    /// Gets the commit entity that the tag points to.
    /// </summary>
    public CommitEntity? Commit
    {
        get
        {
            if (_tag.Target is Commit commit)
            {
                return new CommitEntity(commit, _libGitRepository);
            }

            return null;
        }
    }
}
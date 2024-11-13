using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
/// Represents an annotation entity for a Git tag.
/// </summary>
public class AnnotationEntity
{
    private readonly Repository _libGitRepository;
    
    private readonly TagAnnotation _annotation;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnnotationEntity"/> class.
    /// </summary>
    /// <param name="annotation">The tag annotation.</param>
    /// <param name="repository">The repository.</param>
    public AnnotationEntity(TagAnnotation annotation, Repository repository)
    {
        _annotation = annotation;
        _libGitRepository = repository;
    }

    /// <summary>
    /// Gets the message of the tag annotation.
    /// </summary>
    public string? Message => _annotation.Message;

    /// <summary>
    /// Gets the name of the tag annotation.
    /// </summary>
    public string? Name => _annotation.Name;

    /// <summary>
    /// Gets the SHA of the tag annotation.
    /// </summary>
    public string? Sha => _annotation.Sha;

    /// <summary>
    /// Gets the tagger entity of the tag annotation.
    /// </summary>
    public TaggerEntity? Tagger => _annotation.Tagger != null ? new TaggerEntity(_annotation.Tagger, _libGitRepository) : null;
}
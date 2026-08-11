using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
///     Represents an annotation entity for a Git tag.
/// </summary>
public class AnnotationEntity
{
    private readonly string? _message;
    private readonly string? _name;
    private readonly string? _sha;
    private readonly TaggerEntity? _tagger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AnnotationEntity" /> class.
    /// </summary>
    /// <param name="annotation">The tag annotation.</param>
    /// <param name="repository">The repository.</param>
    public AnnotationEntity(TagAnnotation annotation, Repository repository)
    {
        _message = annotation?.Message;
        _name = annotation?.Name;
        _sha = annotation?.Sha;
        _tagger = annotation?.Tagger is { } tagger
            ? new TaggerEntity(tagger, repository)
            : null;
    }

    internal AnnotationEntity(string? message, string? name, string? sha, TaggerEntity? tagger)
    {
        _message = message;
        _name = name;
        _sha = sha;
        _tagger = tagger;
    }

    /// <summary>
    ///     Gets the message of the tag annotation.
    /// </summary>
    public string? Message => _message;

    /// <summary>
    ///     Gets the name of the tag annotation.
    /// </summary>
    public string? Name => _name;

    /// <summary>
    ///     Gets the SHA of the tag annotation.
    /// </summary>
    public string? Sha => _sha;

    /// <summary>
    ///     Gets the tagger entity of the tag annotation.
    /// </summary>
    public TaggerEntity? Tagger => _tagger;
}

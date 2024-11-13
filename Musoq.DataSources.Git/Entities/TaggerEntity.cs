using System;
using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
/// Represents a tagger entity in a Git repository.
/// </summary>
/// <param name="tagger">The signature object from LibGit2Sharp.</param>
/// <param name="repository">The repository the tagger belongs to.</param>
public class TaggerEntity(Signature tagger, Repository repository)
{
    internal readonly Repository LibGitRepository = repository;
    
    /// <summary>
    /// Gets the name of the tagger.
    /// </summary>
    public string? Name => tagger.Name;

    /// <summary>
    /// Gets the email of the tagger.
    /// </summary>
    public string? Email => tagger.Email;

    /// <summary>
    /// Gets the date and time when the tag was signed.
    /// </summary>
    public DateTimeOffset WhenSigned => tagger.When;
}
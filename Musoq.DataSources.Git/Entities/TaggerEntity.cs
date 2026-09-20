using System;
using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

/// <summary>Represents a detached Git tagger snapshot.</summary>
public class TaggerEntity
{
    private readonly string? _name;
    private readonly string? _email;
    private readonly DateTimeOffset _whenSigned;

    /// <summary>Creates a detached tagger snapshot from a LibGit2Sharp signature.</summary>
    /// <param name="tagger">The tagger signature to copy.</param>
    /// <param name="repository">The source repository; it is accepted for compatibility and not retained.</param>
    public TaggerEntity(Signature tagger, Repository repository)
        : this(tagger?.Name, tagger?.Email, tagger?.When ?? default)
    {
    }

    internal TaggerEntity(string? name, string? email, DateTimeOffset whenSigned)
    {
        _name = name;
        _email = email;
        _whenSigned = whenSigned;
    }

    /// <summary>Gets the tagger display name.</summary>
    public string? Name => _name;

    /// <summary>Gets the tagger email address.</summary>
    public string? Email => _email;

    /// <summary>Gets the time at which the tag was signed.</summary>
    public DateTimeOffset WhenSigned => _whenSigned;
}

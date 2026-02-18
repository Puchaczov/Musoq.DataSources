using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

/// <summary>
///     Represents a wrapper entity for LibGit2Sharp repository information.
///     This class encapsulates various properties describing the state and configuration of a Git repository.
/// </summary>
/// <remarks>
///     This entity provides read-only access to underlying repository information properties.
///     All properties are directly mapped from the wrapped RepositoryInformation object.
/// </remarks>
/// <param name="repositoryInformation">The LibGit2Sharp RepositoryInformation object to wrap.</param>
public class RepositoryInformationEntity(RepositoryInformation repositoryInformation, Repository repository)
{
    internal readonly Repository LibGitRepository = repository;

    /// <summary>
    ///     Gets the path to the Git repository.
    /// </summary>
    /// <value>
    ///     The full path to the Git repository's root directory.
    /// </value>
    public string Path => repositoryInformation.Path;

    /// <summary>
    ///     Gets the working directory path of the Git repository.
    /// </summary>
    /// <value>
    ///     The full path to the repository's working directory where files are checked out.
    /// </value>
    public string WorkingDirectory => repositoryInformation.WorkingDirectory;

    /// <summary>
    ///     Gets a value indicating whether the repository is bare.
    /// </summary>
    /// <value>
    ///     <c>true</c> if the repository is bare (has no working directory); otherwise, <c>false</c>.
    /// </value>
    public bool IsBare => repositoryInformation.IsBare;

    /// <summary>
    ///     Gets a value indicating whether the repository HEAD is detached.
    /// </summary>
    /// <value>
    ///     <c>true</c> if HEAD is detached (not pointing to a branch); otherwise, <c>false</c>.
    /// </value>
    public bool IsHeadDetached => repositoryInformation.IsHeadDetached;

    /// <summary>
    ///     Gets a value indicating whether the repository HEAD is unborn.
    /// </summary>
    /// <value>
    ///     <c>true</c> if HEAD is unborn (no commits yet); otherwise, <c>false</c>.
    /// </value>
    public bool IsHeadUnborn => repositoryInformation.IsHeadUnborn;

    /// <summary>
    ///     Gets a value indicating whether the repository is shallow.
    /// </summary>
    /// <value>
    ///     <c>true</c> if the repository is shallow (has truncated history); otherwise, <c>false</c>.
    /// </value>
    public bool IsShallow => repositoryInformation.IsShallow;
}
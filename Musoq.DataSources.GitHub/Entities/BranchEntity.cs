using Octokit;

namespace Musoq.DataSources.GitHub.Entities;

/// <summary>
///     Represents a GitHub branch entity for querying.
/// </summary>
public class BranchEntity
{
    private readonly Branch _branch;

    /// <summary>
    ///     Initializes a new instance of the BranchEntity class.
    /// </summary>
    /// <param name="branch">The underlying Octokit branch.</param>
    /// <param name="repositoryOwner">The repository owner.</param>
    /// <param name="repositoryName">The repository name.</param>
    public BranchEntity(Branch branch, string repositoryOwner, string repositoryName)
    {
        _branch = branch;
        RepositoryOwner = repositoryOwner;
        RepositoryName = repositoryName;
    }

    /// <summary>
    ///     Gets the branch name.
    /// </summary>
    public string Name => ReadName(_branch);

    /// <summary>
    ///     Gets the commit SHA.
    /// </summary>
    public string CommitSha => ReadCommitSha(_branch);

    /// <summary>
    ///     Gets the commit URL.
    /// </summary>
    public string CommitUrl => ReadCommitUrl(_branch);

    /// <summary>
    ///     Gets whether the branch is protected.
    /// </summary>
    public bool Protected => _branch.Protected;

    /// <summary>
    ///     Gets the repository owner.
    /// </summary>
    public string RepositoryOwner { get; }

    /// <summary>
    ///     Gets the repository name.
    /// </summary>
    public string RepositoryName { get; }

    internal static string ReadName(Branch value) => value.Name;
    internal static string ReadCommitSha(Branch value) => value.Commit?.Sha ?? string.Empty;
    internal static string ReadCommitUrl(Branch value) => value.Commit?.Url ?? string.Empty;
}
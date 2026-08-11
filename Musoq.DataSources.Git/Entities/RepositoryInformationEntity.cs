using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

/// <summary>Represents detached repository information.</summary>
public class RepositoryInformationEntity
{
    private readonly string _path;
    private readonly string _workingDirectory;
    private readonly bool _isBare;
    private readonly bool _isHeadDetached;
    private readonly bool _isHeadUnborn;
    private readonly bool _isShallow;

    /// <summary>Creates a detached repository-information snapshot.</summary>
    /// <param name="repositoryInformation">The information to copy.</param>
    /// <param name="repository">The source repository; it is used only by the compatibility construction path.</param>
    public RepositoryInformationEntity(RepositoryInformation repositoryInformation, Repository repository)
        : this(
            repositoryInformation.Path,
            repositoryInformation.WorkingDirectory,
            repositoryInformation.IsBare,
            repositoryInformation.IsHeadDetached,
            repositoryInformation.IsHeadUnborn,
            repositoryInformation.IsShallow)
    {
    }

    internal RepositoryInformationEntity(
        string path,
        string workingDirectory,
        bool isBare,
        bool isHeadDetached,
        bool isHeadUnborn,
        bool isShallow)
    {
        _path = path;
        _workingDirectory = workingDirectory;
        _isBare = isBare;
        _isHeadDetached = isHeadDetached;
        _isHeadUnborn = isHeadUnborn;
        _isShallow = isShallow;
    }

    /// <summary>Gets the repository metadata path.</summary>
    public string Path => _path;

    /// <summary>Gets the repository working-directory path.</summary>
    public string WorkingDirectory => _workingDirectory;

    /// <summary>Gets whether the repository is bare.</summary>
    public bool IsBare => _isBare;

    /// <summary>Gets whether HEAD is detached.</summary>
    public bool IsHeadDetached => _isHeadDetached;

    /// <summary>Gets whether HEAD is unborn.</summary>
    public bool IsHeadUnborn => _isHeadUnborn;

    /// <summary>Gets whether the repository is shallow.</summary>
    public bool IsShallow => _isShallow;
}

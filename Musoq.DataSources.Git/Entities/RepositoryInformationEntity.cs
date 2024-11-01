using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

public class RepositoryInformationEntity(RepositoryInformation repositoryInformation)
{
    public string Path => repositoryInformation.Path;

    public string WorkingDirectory => repositoryInformation.WorkingDirectory;

    public bool IsBare => repositoryInformation.IsBare;

    public bool IsHeadDetached => repositoryInformation.IsHeadDetached;

    public bool IsHeadUnborn => repositoryInformation.IsHeadUnborn;
    
    public bool IsShallow => repositoryInformation.IsShallow;
}
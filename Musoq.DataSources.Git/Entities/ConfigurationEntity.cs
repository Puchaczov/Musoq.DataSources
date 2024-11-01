using LibGit2Sharp;

namespace Musoq.DataSources.Git.Entities;

public class ConfigurationEntity(Configuration configuration)
{
    private readonly Configuration _configuration = configuration;
}
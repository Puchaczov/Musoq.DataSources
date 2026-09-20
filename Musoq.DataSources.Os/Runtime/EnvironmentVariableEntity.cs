namespace Musoq.DataSources.Os.Runtime;

public sealed class EnvironmentVariableEntity(string name, string target)
{
    public string Name => name;
    public string Target => target;
}

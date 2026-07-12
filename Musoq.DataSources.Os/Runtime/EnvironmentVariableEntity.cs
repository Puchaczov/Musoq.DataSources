namespace Musoq.DataSources.Os.Runtime;

internal sealed class EnvironmentVariableEntity(string name, string target)
{
    public string Name => name;
    public string Target => target;
}

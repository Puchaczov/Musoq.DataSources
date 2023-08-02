namespace Musoq.DataSources.Kubernetes;

public interface IWithLabels
{
    IDictionary<string, string> Labels { get; }
}
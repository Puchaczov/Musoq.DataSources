namespace Musoq.DataSources.Roslyn.Components.NuGet.Http;

internal record Url(string Value)
{
    public static implicit operator string(Url url) => url.Value;

    public static implicit operator Url(string url) => new(url);

    public override string ToString() => Value;
    
    public override int GetHashCode() => Value.GetHashCode();
}
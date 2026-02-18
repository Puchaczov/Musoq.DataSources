namespace Musoq.DataSources.Roslyn.Components.NuGet.Http;

internal record Url(string Value)
{
    public static implicit operator string(Url url)
    {
        return url.Value;
    }

    public static implicit operator Url(string url)
    {
        return new Url(url);
    }

    public override string ToString()
    {
        return Value;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
}
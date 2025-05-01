namespace Musoq.DataSources.Roslyn.Components.NuGet.Version;

internal class Token(TokenType type, string value, int position)
{
    public TokenType Type { get; } = type;
    public string Value { get; } = value;
    
    public int Position { get; } = position;
}
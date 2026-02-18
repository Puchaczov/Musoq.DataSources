namespace Musoq.DataSources.Roslyn.Components.NuGet.Version;

internal enum TokenType
{
    LeftBracket, // [
    RightBracket, // ]
    LeftParenthesis, // (
    RightParenthesis, // )
    Comma, // ,
    VersionNumber, // e.g., 4.8.0
    Wildcard, // *
    Or, // ||
    Eof, // End of input
    Unknown // Unknown
}
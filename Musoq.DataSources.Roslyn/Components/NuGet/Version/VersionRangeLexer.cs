using System.Collections.Generic;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Version;

internal class VersionRangeLexer(string input)
{
    private int _position;
    private const int MaxTokenLength = 1024; // Prevent excessive token lengths

    public IEnumerable<Token> Tokenize()
    {
        if (string.IsNullOrEmpty(input))
        {
            yield return new Token(TokenType.Unknown, string.Empty, 0);
            yield return new Token(TokenType.Eof, string.Empty, 0);
            yield break;
        }

        while (_position < input.Length)
        {
            yield return NextToken();
        }
        
        yield return new Token(TokenType.Eof, string.Empty, _position);
    }
    
    private Token NextToken()
    {
        // Skip whitespace
        while (_position < input.Length && char.IsWhiteSpace(input[_position]))
        {
            _position++;
        }
        
        if (_position >= input.Length)
        {
            return new Token(TokenType.Eof, string.Empty, _position);
        }
        
        var current = input[_position];
        var tokenStart = _position;
        
        switch (current)
        {
            case '[':
                _position++;
                return new Token(TokenType.LeftBracket, "[", tokenStart);
            case ']':
                _position++;
                return new Token(TokenType.RightBracket, "]", tokenStart);
            case '(':
                _position++;
                return new Token(TokenType.LeftParenthesis, "(", tokenStart);
            case ')':
                _position++;
                return new Token(TokenType.RightParenthesis, ")", tokenStart);
            case ',':
                _position++;
                return new Token(TokenType.Comma, ",", tokenStart);
            case '*':
                _position++;
                return new Token(TokenType.Wildcard, "*", tokenStart);
            case '|':
                if (_position + 1 < input.Length && input[_position + 1] == '|')
                {
                    _position += 2;
                    return new Token(TokenType.Or, "||", tokenStart);
                }
                // Handle unexpected character
                _position++;
                return new Token(TokenType.Unknown, input[tokenStart].ToString(), tokenStart);
            default:
                if (char.IsDigit(current) || current == '.')
                {
                    var startPos = _position;
                    var length = 0;
                    
                    while (_position < input.Length && 
                          length < MaxTokenLength &&
                          (char.IsDigit(input[_position]) || 
                           input[_position] == '.' || 
                           input[_position] == '-' || 
                           char.IsLetter(input[_position])))
                    {
                        _position++;
                        length++;
                    }
                    
                    // If we reached max length but there's still more to the token,
                    // consume the rest but don't include it in the token
                    if (length >= MaxTokenLength)
                    {
                        while (_position < input.Length &&
                              (char.IsDigit(input[_position]) || 
                               input[_position] == '.' || 
                               input[_position] == '-' || 
                               char.IsLetter(input[_position])))
                        {
                            _position++;
                        }
                    }
                    
                    return new Token(TokenType.VersionNumber, input.Substring(startPos, length), tokenStart);
                }
                // Handle unexpected character
                _position++;
                return new Token(TokenType.Unknown, input[tokenStart].ToString(), tokenStart);
        }
    }
}

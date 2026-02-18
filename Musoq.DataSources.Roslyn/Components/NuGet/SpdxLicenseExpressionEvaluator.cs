using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components.NuGet;

internal sealed class SpdxLicenseExpressionEvaluator
{
    public static Task<List<string>> GetLicenseIdentifiersAsync(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return Task.FromResult(new List<string>());

        var tokenizer = new SpdxTokenizer(expression);
        var tokens = tokenizer.Tokenize();

        var parser = new SpdxParser(tokens);
        var licenseIds = parser.ExtractLicenseIdentifiers();
        var allIdentifiers = new HashSet<string>(licenseIds);

        foreach (var token in tokens.Where(token => token.Type == TokenType.Identifier))
        {
            if (token.Value is null)
                continue;

            allIdentifiers.Add(token.Value);
        }

        return Task.FromResult(allIdentifiers.ToList());
    }

    private enum TokenType
    {
        Identifier,
        And,
        Or,
        With,
        OpenParen,
        CloseParen,
        EndOfInput
    }

    private class Token(TokenType type, string? value = null)
    {
        public TokenType Type { get; } = type;
        public string? Value { get; } = value;
    }

    private class SpdxTokenizer(string? input)
    {
        private int _position;

        public List<Token> Tokenize()
        {
            var tokens = new List<Token>();
            Token token;

            while ((token = NextToken()).Type != TokenType.EndOfInput) tokens.Add(token);

            tokens.Add(new Token(TokenType.EndOfInput));
            return tokens;
        }

        private Token NextToken()
        {
            while (true)
            {
                SkipWhitespace();

                if (input is null) return new Token(TokenType.EndOfInput);

                if (_position >= input.Length) return new Token(TokenType.EndOfInput);

                if (input[_position] == '(')
                {
                    _position++;
                    return new Token(TokenType.OpenParen, "(");
                }

                if (input[_position] == ')')
                {
                    _position++;
                    return new Token(TokenType.CloseParen, ")");
                }

                if (IsIdentifierStart(input[_position]))
                {
                    var start = _position;
                    while (_position < input.Length && IsIdentifierChar(input[_position])) _position++;

                    var value = input.Substring(start, _position - start);

                    if (value.Equals("AND", StringComparison.OrdinalIgnoreCase)) return new Token(TokenType.And, value);

                    if (value.Equals("OR", StringComparison.OrdinalIgnoreCase)) return new Token(TokenType.Or, value);

                    if (value.Equals("WITH", StringComparison.OrdinalIgnoreCase))
                        return new Token(TokenType.With, value);

                    return new Token(TokenType.Identifier, value);
                }

                _position++;
            }
        }

        private void SkipWhitespace()
        {
            if (input is null) return;

            while (_position < input.Length && char.IsWhiteSpace(input[_position])) _position++;
        }

        private bool IsIdentifierStart(char c)
        {
            return char.IsLetterOrDigit(c) || c == '-' || c == '.' || c == '+' || c == '_';
        }

        private bool IsIdentifierChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '-' || c == '.' || c == '+' || c == '_';
        }
    }

    private class SpdxParser(List<Token> tokens)
    {
        private readonly HashSet<string> _licenseIds = new();
        private int _position;

        public List<string> ExtractLicenseIdentifiers()
        {
            ParseExpression();
            return _licenseIds.ToList();
        }

        private void ParseExpression()
        {
            ParseAndExpression();

            while (CurrentToken().Type == TokenType.Or)
            {
                Consume(TokenType.Or);
                ParseAndExpression();
            }
        }

        private void ParseAndExpression()
        {
            ParseWithExpression();

            while (CurrentToken().Type == TokenType.And)
            {
                Consume(TokenType.And);
                ParseWithExpression();
            }
        }

        private void ParseWithExpression()
        {
            ParsePrimary();

            if (CurrentToken().Type != TokenType.With) return;

            Consume(TokenType.With);

            if (CurrentToken().Type != TokenType.Identifier) return;

            var token = CurrentToken();

            if (token.Value is null)
            {
                _position++;
                return;
            }

            _licenseIds.Add(token.Value);
            Consume(TokenType.Identifier);
        }

        private void ParsePrimary()
        {
            if (CurrentToken().Type == TokenType.OpenParen)
            {
                Consume(TokenType.OpenParen);
                ParseExpression();

                if (CurrentToken().Type == TokenType.CloseParen)
                    Consume(TokenType.CloseParen);
            }
            else if (CurrentToken().Type == TokenType.Identifier)
            {
                var token = CurrentToken();

                if (token.Value is null)
                {
                    _position++;
                    return;
                }

                _licenseIds.Add(token.Value);
                Consume(TokenType.Identifier);
            }
            else if (CurrentToken().Type == TokenType.CloseParen)
            {
                Consume(TokenType.CloseParen);
            }
            else if (CurrentToken().Type != TokenType.EndOfInput)
            {
                _position++;
            }
        }

        private Token CurrentToken()
        {
            return _position < tokens.Count ? tokens[_position] : new Token(TokenType.EndOfInput);
        }

        private void Consume(TokenType expected)
        {
            if (CurrentToken().Type == expected) _position++;
        }
    }
}
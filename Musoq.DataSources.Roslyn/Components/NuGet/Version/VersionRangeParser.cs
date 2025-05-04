using System;
using System.Collections.Generic;
using Musoq.DataSources.Roslyn.Components.NuGet.Version.Ranges;

namespace Musoq.DataSources.Roslyn.Components.NuGet.Version;

internal class VersionRangeParser
{
    private readonly IEnumerator<Token> _tokenEnumerator;
    private Token _currentToken;
    private Token _nextToken;
    private const int MaxRecursionDepth = 1000;
    private int _currentRecursionDepth = 0;
    
    public VersionRangeParser(IEnumerable<Token> tokens)
    {
        if (tokens == null) 
            throw new ArgumentNullException(nameof(tokens));
        
        _tokenEnumerator = tokens.GetEnumerator();
        Advance(); // Sets _currentToken
        Advance(); // Sets _nextToken

        _currentToken ??= new Token(TokenType.Unknown, string.Empty, -1);
        _nextToken ??= _currentToken;
        
        // Check if we started with a valid token
        if (_currentToken.Type == TokenType.Unknown && string.IsNullOrEmpty(_currentToken.Value))
        {
            throw new InvalidOperationException("Empty or invalid version range expression");
        }
    }
    
    private void Advance()
    {
        _currentToken = _nextToken;

        _nextToken = _tokenEnumerator.MoveNext() ? 
            _tokenEnumerator.Current : 
            new Token(TokenType.Eof, string.Empty, -1);
    }
    
    public VersionRange Parse()
    {
        try
        {
            var result = ParseExpression();
            
            if (_currentToken.Type != TokenType.Eof)
            {
                throw new InvalidOperationException($"Unexpected token: {_currentToken.Type} at position {_currentToken.Position}");
            }
            
            return result;
        }
        catch (StackOverflowException)
        {
            throw new InvalidOperationException("Expression is too complex or nested too deeply");
        }
    }
    
    private VersionRange ParseExpression()
    {
        CheckRecursionDepth();
        _currentRecursionDepth++;
        
        try
        {
            var left = ParseRange();
            
            while (_currentToken.Type == TokenType.Or)
            {
                Advance(); // Consume Or token
                var right = ParseRange();
                left = new OrVersionRange(left, right);
            }
            
            return left;
        }
        finally
        {
            _currentRecursionDepth--;
        }
    }
    
    private VersionRange ParseRange()
    {
        CheckRecursionDepth();
        _currentRecursionDepth++;
        
        try
        {
            // Handle exact version or wildcard version
            if (_currentToken.Type == TokenType.VersionNumber)
            {
                var version = _currentToken.Value;
                Advance(); // Consume version number
                
                // Check if there's a wildcard following the version
                if (_currentToken.Type == TokenType.Wildcard)
                {
                    Advance(); // Consume wildcard
                    return new WildcardVersionRange(version);
                }
                
                // Otherwise it's a regular exact version
                if (_currentToken.Type != TokenType.LeftBracket &&
                    _currentToken.Type != TokenType.LeftParenthesis)
                {
                    return new ExactVersionRange(version);
                }
            }
            
            // Handle bracketed ranges
            var inclusiveMin = _currentToken.Type == TokenType.LeftBracket;
            if (_currentToken.Type != TokenType.LeftBracket && _currentToken.Type != TokenType.LeftParenthesis)
            {
                throw new InvalidOperationException($"Expected [ or (, got {_currentToken.Type} at position {_currentToken.Position}");
            }
            
            Advance(); // Consume [ or (
            
            string? minVersion = null;
            if (_currentToken.Type == TokenType.VersionNumber)
            {
                minVersion = _currentToken.Value;
                Advance(); // Consume version number
            }

            bool inclusiveMax;
            // Handle [2.0.323] format (single version with no comma)
            if (_currentToken.Type == TokenType.RightBracket || _currentToken.Type == TokenType.RightParenthesis)
            {
                inclusiveMax = _currentToken.Type == TokenType.RightBracket;
                Advance(); // Consume ] or )
                
                // Use the same version for both min and max
                return new RangeVersionRange(minVersion, inclusiveMin, minVersion, inclusiveMax);
            }
            
            if (_currentToken.Type != TokenType.Comma)
            {
                throw new InvalidOperationException($"Expected comma, got {_currentToken.Type} at position {_currentToken.Position}");
            }
            
            Advance(); // Consume comma
            
            string? maxVersion = null;
            if (_currentToken.Type == TokenType.VersionNumber)
            {
                maxVersion = _currentToken.Value;
                Advance(); // Consume version number
            }
            
            inclusiveMax = _currentToken.Type == TokenType.RightBracket;
            if (_currentToken.Type != TokenType.RightBracket && _currentToken.Type != TokenType.RightParenthesis)
            {
                throw new InvalidOperationException($"Expected ] or ), got {_currentToken.Type} at position {_currentToken.Position}");
            }
            
            Advance(); // Consume ] or )
            
            return new RangeVersionRange(minVersion, inclusiveMin, maxVersion, inclusiveMax);
        }
        finally
        {
            _currentRecursionDepth--;
        }
    }
    
    private void CheckRecursionDepth()
    {
        if (_currentRecursionDepth >= MaxRecursionDepth)
        {
            throw new InvalidOperationException("Expression is too complex or nested too deeply");
        }
    }
}

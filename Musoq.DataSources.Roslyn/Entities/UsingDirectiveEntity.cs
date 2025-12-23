using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents a using directive entity that provides information about an import statement in the source code.
/// </summary>
public class UsingDirectiveEntity
{
    private readonly UsingDirectiveSyntax _syntax;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsingDirectiveEntity"/> class.
    /// </summary>
    /// <param name="syntax">The using directive syntax node.</param>
    public UsingDirectiveEntity(UsingDirectiveSyntax syntax)
    {
        _syntax = syntax;
    }

    /// <summary>
    /// Gets the name/namespace being imported.
    /// </summary>
    public string? Name => _syntax.Name?.ToString();

    /// <summary>
    /// Gets a value indicating whether this is a static using directive.
    /// </summary>
    public bool IsStatic => _syntax.StaticKeyword != default && !_syntax.StaticKeyword.IsMissing;

    /// <summary>
    /// Gets a value indicating whether this is a global using directive.
    /// </summary>
    public bool IsGlobal => _syntax.GlobalKeyword != default && !_syntax.GlobalKeyword.IsMissing;

    /// <summary>
    /// Gets the alias if this is an alias using directive, null otherwise.
    /// </summary>
    public string? Alias => _syntax.Alias?.Name.Identifier.Text;

    /// <summary>
    /// Gets a value indicating whether this is an alias using directive.
    /// </summary>
    public bool HasAlias => _syntax.Alias != null;

    /// <summary>
    /// Gets a value indicating whether this is an unsafe using directive.
    /// </summary>
    public bool IsUnsafe => _syntax.UnsafeKeyword != default && !_syntax.UnsafeKeyword.IsMissing;

    /// <summary>
    /// Gets the line number where this using directive appears.
    /// </summary>
    public int LineNumber
    {
        get
        {
            var lineSpan = _syntax.SyntaxTree.GetLineSpan(_syntax.Span);
            return lineSpan.StartLinePosition.Line + 1;
        }
    }

    /// <summary>
    /// Returns a string representation of the using directive.
    /// </summary>
    /// <returns>A string representing the using directive.</returns>
    public override string ToString() => _syntax.ToString();
}

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents a constructor entity that provides information about a constructor in the source code.
/// </summary>
public class ConstructorEntity
{
    private readonly IMethodSymbol _constructorSymbol;
    private readonly ConstructorDeclarationSyntax? _syntax;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConstructorEntity"/> class.
    /// </summary>
    /// <param name="constructorSymbol">The method symbol representing the constructor.</param>
    /// <param name="syntax">The constructor declaration syntax node.</param>
    public ConstructorEntity(IMethodSymbol constructorSymbol, ConstructorDeclarationSyntax? syntax = null)
    {
        _constructorSymbol = constructorSymbol;
        _syntax = syntax;
    }

    /// <summary>
    /// Gets the name of the containing type (class name).
    /// </summary>
    public string Name => _constructorSymbol.ContainingType.Name;

    /// <summary>
    /// Gets a value indicating whether this is a static constructor.
    /// </summary>
    public bool IsStatic => _constructorSymbol.IsStatic;

    /// <summary>
    /// Gets a value indicating whether this constructor is implicitly declared (default constructor).
    /// </summary>
    public bool IsImplicitlyDeclared => _constructorSymbol.IsImplicitlyDeclared;

    /// <summary>
    /// Gets a value indicating whether this is a primary constructor.
    /// </summary>
    public bool IsPrimary => _syntax == null && !_constructorSymbol.IsImplicitlyDeclared;

    /// <summary>
    /// Gets the accessibility of the constructor (public, private, etc.).
    /// </summary>
    public string Accessibility => _constructorSymbol.DeclaredAccessibility.ToString().ToLowerInvariant();

    /// <summary>
    /// Gets the number of parameters.
    /// </summary>
    public int ParameterCount => _constructorSymbol.Parameters.Length;

    /// <summary>
    /// Gets the parameters of the constructor.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<ParameterEntity> Parameters => 
        _constructorSymbol.Parameters.Select(p => new ParameterEntity(p));

    /// <summary>
    /// Gets the modifiers of the constructor.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> Modifiers
    {
        get
        {
            if (_syntax == null)
                return [];

            return _syntax.Modifiers
                .Where(token => 
                    token.IsKind(SyntaxKind.PublicKeyword) ||
                    token.IsKind(SyntaxKind.PrivateKeyword) ||
                    token.IsKind(SyntaxKind.ProtectedKeyword) ||
                    token.IsKind(SyntaxKind.InternalKeyword) ||
                    token.IsKind(SyntaxKind.StaticKeyword))
                .Select(token => token.ValueText);
        }
    }

    /// <summary>
    /// Gets the attributes applied to the constructor.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<AttributeEntity> Attributes => 
        _constructorSymbol.GetAttributes().Select(attr => new AttributeEntity(attr));

    /// <summary>
    /// Gets a value indicating whether the constructor has a body.
    /// </summary>
    public bool HasBody => _syntax?.Body != null || _syntax?.ExpressionBody != null;

    /// <summary>
    /// Gets the number of statements in the constructor body.
    /// </summary>
    public int StatementsCount => _syntax?.Body?.Statements.Count ?? 0;

    /// <summary>
    /// Gets the body text of the constructor.
    /// </summary>
    public string? Text => _syntax?.ToFullString();

    /// <summary>
    /// Gets the lines of code for this constructor.
    /// </summary>
    public int LinesOfCode
    {
        get
        {
            if (_syntax == null)
                return 0;

            var lineSpan = _syntax.SyntaxTree.GetLineSpan(_syntax.Span);
            return lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this constructor calls another constructor (this() or base()).
    /// </summary>
    public bool HasInitializer => _syntax?.Initializer != null;

    /// <summary>
    /// Gets the kind of initializer if present ("this" or "base").
    /// </summary>
    public string? InitializerKind
    {
        get
        {
            if (_syntax?.Initializer == null)
                return null;

            return _syntax.Initializer.ThisOrBaseKeyword.IsKind(SyntaxKind.ThisKeyword) ? "this" : "base";
        }
    }

    /// <summary>
    /// Returns a string representation of the constructor entity.
    /// </summary>
    /// <returns>A string representing the constructor.</returns>
    public override string ToString()
    {
        var parameters = string.Join(", ", Parameters.Select(p => $"{p.Type} {p.Name}"));
        var modifiers = string.Join(" ", Modifiers);
        return $"{modifiers} {Name}({parameters})".Trim();
    }
}

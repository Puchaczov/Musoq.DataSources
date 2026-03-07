using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
///     Represents a delegate declaration entity.
/// </summary>
public class DelegateEntity
{
    private readonly DelegateDeclarationSyntax _syntax;
    private readonly INamedTypeSymbol _symbol;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DelegateEntity" /> class.
    /// </summary>
    /// <param name="symbol">The named type symbol representing the delegate.</param>
    /// <param name="syntax">The delegate declaration syntax node.</param>
    public DelegateEntity(INamedTypeSymbol symbol, DelegateDeclarationSyntax syntax)
    {
        _symbol = symbol;
        _syntax = syntax;
    }

    /// <summary>
    ///     Gets the name of the delegate.
    /// </summary>
    public string Name => _symbol.Name;

    /// <summary>
    ///     Gets the full name of the delegate including namespace.
    /// </summary>
    public string FullName => _symbol.ToDisplayString();

    /// <summary>
    ///     Gets the namespace of the delegate.
    /// </summary>
    public string Namespace => _symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

    /// <summary>
    ///     Gets the return type of the delegate.
    /// </summary>
    public string ReturnType => _symbol.DelegateInvokeMethod?.ReturnType.Name ?? string.Empty;

    /// <summary>
    ///     Gets the full return type name including namespace.
    /// </summary>
    public string FullReturnType => _symbol.DelegateInvokeMethod?.ReturnType.ToDisplayString() ?? string.Empty;

    /// <summary>
    ///     Gets the number of parameters.
    /// </summary>
    public int ParameterCount => _symbol.DelegateInvokeMethod?.Parameters.Length ?? 0;

    /// <summary>
    ///     Gets the parameters of the delegate.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<ParameterEntity> Parameters =>
        _symbol.DelegateInvokeMethod?.Parameters.Select(p => new ParameterEntity(p))
        ?? Enumerable.Empty<ParameterEntity>();

    /// <summary>
    ///     Gets the type parameters of the delegate.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> TypeParameters => _symbol.TypeParameters.Select(tp => tp.Name);

    /// <summary>
    ///     Gets the number of type parameters.
    /// </summary>
    public int TypeParameterCount => _symbol.TypeParameters.Length;

    /// <summary>
    ///     Gets a value indicating whether the delegate is generic.
    /// </summary>
    public bool IsGeneric => _symbol.IsGenericType;

    /// <summary>
    ///     Gets the accessibility of the delegate (public, private, etc.).
    /// </summary>
    public string Accessibility => _symbol.DeclaredAccessibility.ToString().ToLowerInvariant();

    /// <summary>
    ///     Gets the modifiers of the delegate.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> Modifiers => _syntax.Modifiers
        .Where(token =>
            token.IsKind(SyntaxKind.PublicKeyword) ||
            token.IsKind(SyntaxKind.PrivateKeyword) ||
            token.IsKind(SyntaxKind.ProtectedKeyword) ||
            token.IsKind(SyntaxKind.InternalKeyword))
        .Select(token => token.ValueText);

    /// <summary>
    ///     Gets the attributes applied to the delegate.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<AttributeEntity> Attributes =>
        _symbol.GetAttributes().Select(attr => new AttributeEntity(attr));

    /// <summary>
    ///     Gets a value indicating whether the delegate has XML documentation.
    /// </summary>
    public bool HasDocumentation
    {
        get
        {
            var trivia = _syntax.GetLeadingTrivia();
            return trivia.Any(t =>
                t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
        }
    }

    /// <summary>
    ///     Returns a string representation of the delegate entity.
    /// </summary>
    public override string ToString()
    {
        var parameters = string.Join(", ", Parameters.Select(p => $"{p.Type} {p.Name}"));
        return $"delegate {ReturnType} {Name}({parameters})";
    }
}

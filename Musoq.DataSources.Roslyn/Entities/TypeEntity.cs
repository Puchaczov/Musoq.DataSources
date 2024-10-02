using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents an abstract base class for type entities in the Roslyn data source.
/// </summary>
/// <param name="symbol">The symbol representing the named type.</param>
public abstract class TypeEntity(INamedTypeSymbol symbol)
{
    /// <summary>
    /// The symbol representing the named type.
    /// </summary>
    protected readonly INamedTypeSymbol Symbol = symbol;

    /// <summary>
    /// Gets the name of the type.
    /// </summary>
    public string Name => Symbol.Name;

    /// <summary>
    /// Gets the full name of the type.
    /// </summary>
    public string FullName => $"{Namespace}.{Name}";

    /// <summary>
    /// Gets the namespace of the type.
    /// </summary>
    public string Namespace => Symbol.ContainingNamespace.ToDisplayString();

    /// <summary>
    /// Gets the modifiers of the type (e.g., public, private, protected, internal, abstract, sealed).
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> Modifiers => Symbol.DeclaringSyntaxReferences
        .FirstOrDefault()?.GetSyntax()
        .ChildTokens()
        .Where(token => token.IsKind(SyntaxKind.PublicKeyword) ||
                        token.IsKind(SyntaxKind.PrivateKeyword) ||
                        token.IsKind(SyntaxKind.ProtectedKeyword) ||
                        token.IsKind(SyntaxKind.InternalKeyword) ||
                        token.IsKind(SyntaxKind.AbstractKeyword) ||
                        token.IsKind(SyntaxKind.SealedKeyword))
        .Select(token => token.ValueText) ?? Array.Empty<string>();

    /// <summary>
    /// Gets the methods of the type.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<MethodEntity> Methods => Symbol.GetMembers()
        .OfType<IMethodSymbol>()
        .Where(m => !m.IsImplicitlyDeclared)
        .Where(m => m.MethodKind != MethodKind.PropertyGet && m.MethodKind != MethodKind.PropertySet)
        .Select(m => new MethodEntity(m));

    /// <summary>
    /// Gets the properties of the type.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<PropertyEntity> Properties => Symbol.GetMembers()
        .OfType<IPropertySymbol>()
        .Select(p => new PropertyEntity(p));
}
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
///     Represents an abstract base class for type entities in the Roslyn data source.
/// </summary>
/// <param name="symbol">The symbol representing the named type.</param>
public abstract class TypeEntity(INamedTypeSymbol symbol)
{
    /// <summary>
    ///     Gets the name of the type.
    /// </summary>
    public string Name => symbol.Name;

    /// <summary>
    ///     Gets the full name of the type.
    /// </summary>
    public string FullName => $"{Namespace}.{Name}";

    /// <summary>
    ///     Gets the namespace of the type.
    /// </summary>
    public string Namespace => symbol.ContainingNamespace.ToDisplayString();

    /// <summary>
    ///     Gets a value indicating whether the type is an interface.
    /// </summary>
    public bool IsInterface => symbol.TypeKind == TypeKind.Interface;

    /// <summary>
    ///     Gets a value indicating whether the type is an enum.
    /// </summary>
    public bool IsEnum => symbol.TypeKind == TypeKind.Enum;

    /// <summary>
    ///     Gets a value indicating whether the type is a class.
    /// </summary>
    public bool IsClass => symbol.TypeKind == TypeKind.Class;

    /// <summary>
    ///     Gets a value indicating whether the type is a struct.
    /// </summary>
    public bool IsStruct => symbol.TypeKind == TypeKind.Struct;

    /// <summary>
    ///     Gets a value indicating whether the type is abstract.
    /// </summary>
    public bool IsAbstract => symbol.IsAbstract;

    /// <summary>
    ///     Gets a value indicating whether the type is sealed.
    /// </summary>
    public bool IsSealed => symbol.IsSealed;

    /// <summary>
    ///     Gets a value indicating whether the type is static.
    /// </summary>
    public bool IsStatic => symbol.IsStatic;

    /// <summary>
    ///     Gets a value indicating whether the type is nested.
    /// </summary>
    public bool IsNested => symbol.ContainingType != null;

    /// <summary>
    ///     Gets a value indicating whether the type is generic.
    /// </summary>
    public bool IsGeneric => symbol.IsGenericType;

    /// <summary>
    ///     Gets the modifiers of the type (e.g., public, private, protected, internal, abstract, sealed).
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> Modifiers => symbol.DeclaringSyntaxReferences
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
    ///     Gets the methods of the type.
    /// </summary>
    [BindablePropertyAsTable]
    public abstract IEnumerable<MethodEntity> Methods { get; }

    /// <summary>
    ///     Gets the properties of the type.
    /// </summary>
    [BindablePropertyAsTable]
    public abstract IEnumerable<PropertyEntity> Properties { get; }
}
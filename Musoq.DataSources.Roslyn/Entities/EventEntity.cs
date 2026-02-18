using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
///     Represents an event entity that provides information about an event in the source code.
/// </summary>
public class EventEntity
{
    private readonly IEventSymbol _eventSymbol;
    private readonly EventFieldDeclarationSyntax? _fieldSyntax;
    private readonly EventDeclarationSyntax? _syntax;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EventEntity" /> class.
    /// </summary>
    /// <param name="eventSymbol">The event symbol representing the event.</param>
    /// <param name="syntax">The event declaration syntax node (for custom add/remove).</param>
    /// <param name="fieldSyntax">The event field declaration syntax node (for field-like events).</param>
    public EventEntity(IEventSymbol eventSymbol, EventDeclarationSyntax? syntax = null,
        EventFieldDeclarationSyntax? fieldSyntax = null)
    {
        _eventSymbol = eventSymbol;
        _syntax = syntax;
        _fieldSyntax = fieldSyntax;
    }

    /// <summary>
    ///     Gets the name of the event.
    /// </summary>
    public string Name => _eventSymbol.Name;

    /// <summary>
    ///     Gets the type of the event (delegate type).
    /// </summary>
    public string Type
    {
        get
        {
            var type = _eventSymbol.Type;
            if (type is INamedTypeSymbol namedType &&
                namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                return namedType.TypeArguments.FirstOrDefault()?.Name ?? type.Name;
            return type.Name;
        }
    }

    /// <summary>
    ///     Gets the full type name including namespace.
    /// </summary>
    public string FullTypeName => _eventSymbol.Type.ToDisplayString();

    /// <summary>
    ///     Gets a value indicating whether the event is static.
    /// </summary>
    public bool IsStatic => _eventSymbol.IsStatic;

    /// <summary>
    ///     Gets a value indicating whether the event is virtual.
    /// </summary>
    public bool IsVirtual => _eventSymbol.IsVirtual;

    /// <summary>
    ///     Gets a value indicating whether the event is abstract.
    /// </summary>
    public bool IsAbstract => _eventSymbol.IsAbstract;

    /// <summary>
    ///     Gets a value indicating whether the event is override.
    /// </summary>
    public bool IsOverride => _eventSymbol.IsOverride;

    /// <summary>
    ///     Gets a value indicating whether the event is sealed.
    /// </summary>
    public bool IsSealed => _eventSymbol.IsSealed;

    /// <summary>
    ///     Gets the accessibility of the event (public, private, etc.).
    /// </summary>
    public string Accessibility => _eventSymbol.DeclaredAccessibility.ToString().ToLowerInvariant();

    /// <summary>
    ///     Gets a value indicating whether the event has explicit add/remove accessors.
    /// </summary>
    public bool HasExplicitAccessors => _syntax?.AccessorList != null;

    /// <summary>
    ///     Gets a value indicating whether this is a field-like event declaration.
    /// </summary>
    public bool IsFieldLike => _fieldSyntax != null;

    /// <summary>
    ///     Gets a value indicating whether the event is implicitly declared.
    /// </summary>
    public bool IsImplicitlyDeclared => _eventSymbol.IsImplicitlyDeclared;

    /// <summary>
    ///     Gets the modifiers of the event.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> Modifiers
    {
        get
        {
            SyntaxTokenList? modifiers = null;

            if (_syntax != null)
                modifiers = _syntax.Modifiers;
            else if (_fieldSyntax != null)
                modifiers = _fieldSyntax.Modifiers;

            if (modifiers == null)
                return [];

            return modifiers.Value
                .Where(token =>
                    token.IsKind(SyntaxKind.PublicKeyword) ||
                    token.IsKind(SyntaxKind.PrivateKeyword) ||
                    token.IsKind(SyntaxKind.ProtectedKeyword) ||
                    token.IsKind(SyntaxKind.InternalKeyword) ||
                    token.IsKind(SyntaxKind.StaticKeyword) ||
                    token.IsKind(SyntaxKind.VirtualKeyword) ||
                    token.IsKind(SyntaxKind.AbstractKeyword) ||
                    token.IsKind(SyntaxKind.OverrideKeyword) ||
                    token.IsKind(SyntaxKind.SealedKeyword) ||
                    token.IsKind(SyntaxKind.NewKeyword))
                .Select(token => token.ValueText);
        }
    }

    /// <summary>
    ///     Gets the attributes applied to the event.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<AttributeEntity> Attributes =>
        _eventSymbol.GetAttributes().Select(attr => new AttributeEntity(attr));

    /// <summary>
    ///     Returns a string representation of the event entity.
    /// </summary>
    /// <returns>A string representing the event.</returns>
    public override string ToString()
    {
        var modifiers = string.Join(" ", Modifiers);
        return $"{modifiers} event {Type} {Name}".Trim();
    }
}
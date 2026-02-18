using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
///     Represents a property entity in the Roslyn data source.
/// </summary>
public class PropertyEntity
{
    private readonly PropertyDeclarationSyntax? _propertyDeclaration;
    private readonly IPropertySymbol _propertySymbol;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PropertyEntity" /> class.
    /// </summary>
    /// <param name="propertySymbol">The property symbol representing the property.</param>
    public PropertyEntity(IPropertySymbol propertySymbol)
    {
        _propertySymbol = propertySymbol;

        // Get the syntax node from the symbol
        var syntaxReference = propertySymbol.DeclaringSyntaxReferences.FirstOrDefault();
        _propertyDeclaration = syntaxReference?.GetSyntax() as PropertyDeclarationSyntax;
    }

    /// <summary>
    ///     Gets the name of the property.
    /// </summary>
    public string Name => _propertySymbol.Name;

    /// <summary>
    ///     Gets the type of the property as a display string.
    /// </summary>
    public string Type => _propertySymbol.Type.Name;

    /// <summary>
    ///     Gets a value indicating whether the property is an indexer.
    /// </summary>
    public bool IsIndexer => _propertySymbol.IsIndexer;

    /// <summary>
    ///     Gets a value indicating whether the property is read-only.
    /// </summary>
    public bool IsReadOnly => _propertySymbol.IsReadOnly;

    /// <summary>
    ///     Gets a value indicating whether the property is write-only.
    /// </summary>
    public bool IsWriteOnly => _propertySymbol.IsWriteOnly;

    /// <summary>
    ///     Gets a value indicating whether the property is required.
    /// </summary>
    public bool IsRequired => _propertySymbol.IsRequired;

    /// <summary>
    ///     Gets a value indicating whether the property is associated with events.
    /// </summary>
    public bool IsWithEvents => _propertySymbol.IsWithEvents;

    /// <summary>
    ///     Gets a value indicating whether the property is virtual.
    /// </summary>
    public bool IsVirtual => _propertySymbol.IsVirtual;

    /// <summary>
    ///     Gets a value indicating whether the property is an override.
    /// </summary>
    public bool IsOverride => _propertySymbol.IsOverride;

    /// <summary>
    ///     Gets a value indicating whether the property is abstract.
    /// </summary>
    public bool IsAbstract => _propertySymbol.IsAbstract;

    /// <summary>
    ///     Gets a value indicating whether the property is sealed.
    /// </summary>
    public bool IsSealed => _propertySymbol.IsSealed;

    /// <summary>
    ///     Gets a value indicating whether the property is static.
    /// </summary>
    public bool IsStatic => _propertySymbol.IsStatic;

    /// <summary>
    ///     Gets the modifiers of the property (e.g., public, private, protected).
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> Modifiers => _propertySymbol.DeclaringSyntaxReferences
        .FirstOrDefault()?.GetSyntax()
        .ChildTokens()
        .Where(token => token.IsKind(SyntaxKind.PublicKeyword) ||
                        token.IsKind(SyntaxKind.PrivateKeyword) ||
                        token.IsKind(SyntaxKind.ProtectedKeyword) ||
                        token.IsKind(SyntaxKind.InternalKeyword) ||
                        token.IsKind(SyntaxKind.AbstractKeyword) ||
                        token.IsKind(SyntaxKind.SealedKeyword))
        .Select(token => token.ValueText) ?? [];

    /// <summary>
    ///     Gets a value indicating whether the property is an auto-implemented property.
    ///     Returns true for properties with no explicit getter/setter body.
    /// </summary>
    public bool IsAutoProperty
    {
        get
        {
            if (_propertyDeclaration?.AccessorList == null)
                // Properties without accessor lists (expression-bodied properties, or properties
                // without syntax references) are not auto-properties
                return false;

            foreach (var accessor in _propertyDeclaration.AccessorList.Accessors)
                // If any accessor has a body or expression body, it's not an auto-property
                if (accessor.Body != null || accessor.ExpressionBody != null)
                    return false;

            return true;
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the property has a get accessor.
    /// </summary>
    public bool HasGetter
    {
        get
        {
            if (_propertyDeclaration?.AccessorList != null)
                return _propertyDeclaration.AccessorList.Accessors
                    .Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));

            // Expression-bodied properties have an implicit getter
            if (_propertyDeclaration?.ExpressionBody != null) return true;

            return false;
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the property has a set accessor (includes init).
    /// </summary>
    public bool HasSetter
    {
        get
        {
            if (_propertyDeclaration?.AccessorList == null) return false;

            return _propertyDeclaration.AccessorList.Accessors
                .Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration) ||
                          a.IsKind(SyntaxKind.InitAccessorDeclaration));
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the property has an init accessor specifically.
    /// </summary>
    public bool HasInitSetter
    {
        get
        {
            if (_propertyDeclaration?.AccessorList == null) return false;

            return _propertyDeclaration.AccessorList.Accessors
                .Any(a => a.IsKind(SyntaxKind.InitAccessorDeclaration));
        }
    }

    /// <summary>
    ///     Gets the accessibility of the property (public, private, etc.).
    /// </summary>
    public string Accessibility => _propertySymbol.DeclaredAccessibility.ToString().ToLowerInvariant();
}
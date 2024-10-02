using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents a property entity in the Roslyn data source.
/// </summary>
public class PropertyEntity
{
    private readonly IPropertySymbol _propertySymbol;

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyEntity"/> class.
    /// </summary>
    /// <param name="propertySymbol">The property symbol representing the property.</param>
    public PropertyEntity(IPropertySymbol propertySymbol)
    {
        _propertySymbol = propertySymbol;
    }

    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    public string Name => _propertySymbol.Name;

    /// <summary>
    /// Gets the type of the property as a display string.
    /// </summary>
    public string Type => _propertySymbol.Type.Name;

    /// <summary>
    /// Gets a value indicating whether the property is an indexer.
    /// </summary>
    public bool IsIndexer => _propertySymbol.IsIndexer;

    /// <summary>
    /// Gets a value indicating whether the property is read-only.
    /// </summary>
    public bool IsReadOnly => _propertySymbol.IsReadOnly;

    /// <summary>
    /// Gets a value indicating whether the property is write-only.
    /// </summary>
    public bool IsWriteOnly => _propertySymbol.IsWriteOnly;

    /// <summary>
    /// Gets a value indicating whether the property is required.
    /// </summary>
    public bool IsRequired => _propertySymbol.IsRequired;

    /// <summary>
    /// Gets a value indicating whether the property is associated with events.
    /// </summary>
    public bool IsWithEvents => _propertySymbol.IsWithEvents;

    /// <summary>
    /// Gets a value indicating whether the property is virtual.
    /// </summary>
    public bool IsVirtual => _propertySymbol.IsVirtual;

    /// <summary>
    /// Gets a value indicating whether the property is an override.
    /// </summary>
    public bool IsOverride => _propertySymbol.IsOverride;

    /// <summary>
    /// Gets a value indicating whether the property is abstract.
    /// </summary>
    public bool IsAbstract => _propertySymbol.IsAbstract;

    /// <summary>
    /// Gets a value indicating whether the property is sealed.
    /// </summary>
    public bool IsSealed => _propertySymbol.IsSealed;
    
    /// <summary>
    /// Gets a value indicating whether the property is static.
    /// </summary>
    public bool IsStatic => _propertySymbol.IsStatic;

    /// <summary>
    /// Gets the modifiers of the property (e.g., public, private, protected).
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
}
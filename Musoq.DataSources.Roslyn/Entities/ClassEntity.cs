using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents a class entity that provides information about a class in the source code.
/// </summary>
public class ClassEntity : TypeEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClassEntity"/> class.
    /// </summary>
    /// <param name="symbol">The named type symbol from Roslyn.</param>
    public ClassEntity(INamedTypeSymbol symbol)
        : base(symbol)
    {
        //get string of the class
    }

    /// <summary>
    /// Gets the text of the class.
    /// </summary>
    public string Text => Symbol.ToDisplayString();

    /// <summary>
    /// Gets a value indicating whether the class is abstract.
    /// </summary>
    public bool IsAbstract => Symbol.IsAbstract;

    /// <summary>
    /// Gets a value indicating whether the class is sealed.
    /// </summary>
    public bool IsSealed => Symbol.IsSealed;
    
    /// <summary>
    /// Gets a value indicating whether the class is static.
    /// </summary>
    public bool IsStatic => Symbol.IsStatic;

    /// <summary>
    /// Gets the base types of the class.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> BaseTypes => Symbol.BaseType != null ? [Symbol.BaseType.Name] : [];

    /// <summary>
    /// Gets the interfaces implemented by the class.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> Interfaces => Symbol.Interfaces.Select(i => i.Name);

    /// <summary>
    /// Gets the type parameters of the class.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> TypeParameters => Symbol.TypeParameters.Select(p => p.Name);

    /// <summary>
    /// Gets the names of the members of the class.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> MemberNames => Symbol.GetMembers().Select(m => m.Name);

    /// <summary>
    /// Gets the attributes of the class.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<AttributeEntity> Attributes => Symbol.GetAttributes().Select(a => new AttributeEntity(a));
}
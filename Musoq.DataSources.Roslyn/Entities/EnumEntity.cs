using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents an enumeration entity derived from a type entity.
/// </summary>
/// <param name="symbol">The symbol representing the named type.</param>
public class EnumEntity(INamedTypeSymbol symbol) : TypeEntity(symbol)
{
    /// <summary>
    /// Gets the members of the enumeration.
    /// </summary>
    /// <value>
    /// An enumerable collection of member names.
    /// </value>
    [BindablePropertyAsTable]
    public IEnumerable<string> Members => Symbol
        .GetMembers()
        .OfType<IFieldSymbol>()
        .Where(f => f.ConstantValue != null)
        .Select(f => f.Name);
}
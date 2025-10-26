using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents an attribute entity that provides information about an attribute in the source code.
/// </summary>
public class AttributeEntity
{
    private const string AttributeSuffix = "Attribute";
    private readonly AttributeData _attributeData;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttributeEntity"/> class.
    /// </summary>
    /// <param name="attributeData">The attribute data from Roslyn.</param>
    public AttributeEntity(AttributeData attributeData)
    {
        _attributeData = attributeData;
    }

    /// <summary>
    /// Gets the name of the attribute.
    /// </summary>
    public string? Name
    {
        get
        {
            var name = _attributeData.AttributeClass?.Name;
            if (name?.EndsWith(AttributeSuffix) == true && name.Length > AttributeSuffix.Length)
            {
                return name.Substring(0, name.Length - AttributeSuffix.Length);
            }
            return name;
        }
    }

    /// <summary>
    /// Gets the constructor arguments of the attribute as strings.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> ConstructorArguments =>
        _attributeData.ConstructorArguments.Select(arg => arg.ToCSharpString());

    /// <summary>
    /// Gets the named arguments of the attribute as key-value pairs.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<KeyValuePair<string, string>> NamedArguments =>
        _attributeData.NamedArguments.Select(na =>
            new KeyValuePair<string, string>(na.Key, na.Value.ToCSharpString()));

    /// <summary>
    /// Returns a string representation of the attribute entity.
    /// </summary>
    /// <returns>A string that represents the attribute entity.</returns>
    public override string ToString()
    {
        var args = ConstructorArguments.Concat(NamedArguments.Select(na => $"{na.Key} = {na.Value}"));
        return $"[{Name}({string.Join(", ", args)})]";
    }
}
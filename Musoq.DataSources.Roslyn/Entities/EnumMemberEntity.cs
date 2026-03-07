using Microsoft.CodeAnalysis;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
///     Represents an enum member (constant field) entity.
/// </summary>
public class EnumMemberEntity
{
    private readonly IFieldSymbol _fieldSymbol;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EnumMemberEntity" /> class.
    /// </summary>
    /// <param name="fieldSymbol">The field symbol representing the enum member.</param>
    public EnumMemberEntity(IFieldSymbol fieldSymbol)
    {
        _fieldSymbol = fieldSymbol;
    }

    /// <summary>
    ///     Gets the name of the enum member.
    /// </summary>
    public string Name => _fieldSymbol.Name;

    /// <summary>
    ///     Gets the constant value of the enum member as a string.
    /// </summary>
    public string? Value => _fieldSymbol.ConstantValue?.ToString();

    /// <summary>
    ///     Gets the constant value of the enum member as an object.
    /// </summary>
    public object? ConstantValue => _fieldSymbol.HasConstantValue ? _fieldSymbol.ConstantValue : null;

    /// <summary>
    ///     Returns a string representation of the enum member.
    /// </summary>
    /// <returns>A string representing the enum member.</returns>
    public override string ToString()
    {
        return $"{Name} = {Value}";
    }
}

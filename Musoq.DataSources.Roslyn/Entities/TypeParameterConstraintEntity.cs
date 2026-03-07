using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
///     Represents a type parameter constraint entity.
/// </summary>
public class TypeParameterConstraintEntity
{
    private readonly ITypeParameterSymbol _typeParameter;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TypeParameterConstraintEntity" /> class.
    /// </summary>
    /// <param name="typeParameter">The type parameter symbol.</param>
    public TypeParameterConstraintEntity(ITypeParameterSymbol typeParameter)
    {
        _typeParameter = typeParameter;
    }

    /// <summary>
    ///     Gets the name of the type parameter (e.g., T, TKey, TValue).
    /// </summary>
    public string Name => _typeParameter.Name;

    /// <summary>
    ///     Gets the ordinal position of the type parameter.
    /// </summary>
    public int Ordinal => _typeParameter.Ordinal;

    /// <summary>
    ///     Gets a value indicating whether the type parameter has the 'class' constraint.
    /// </summary>
    public bool HasReferenceTypeConstraint => _typeParameter.HasReferenceTypeConstraint;

    /// <summary>
    ///     Gets a value indicating whether the type parameter has the 'struct' constraint.
    /// </summary>
    public bool HasValueTypeConstraint => _typeParameter.HasValueTypeConstraint;

    /// <summary>
    ///     Gets a value indicating whether the type parameter has the 'unmanaged' constraint.
    /// </summary>
    public bool HasUnmanagedTypeConstraint => _typeParameter.HasUnmanagedTypeConstraint;

    /// <summary>
    ///     Gets a value indicating whether the type parameter has the 'notnull' constraint.
    /// </summary>
    public bool HasNotNullConstraint => _typeParameter.HasNotNullConstraint;

    /// <summary>
    ///     Gets a value indicating whether the type parameter has the 'new()' constraint.
    /// </summary>
    public bool HasConstructorConstraint => _typeParameter.HasConstructorConstraint;

    /// <summary>
    ///     Gets the constraint types (base class and interface constraints) as fully qualified names.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> ConstraintTypes =>
        _typeParameter.ConstraintTypes.Select(t => t.ToDisplayString());

    /// <summary>
    ///     Gets a summary of all constraints as a string (e.g., "where T : class, IDisposable, new()").
    /// </summary>
    public string ConstraintSummary
    {
        get
        {
            var parts = new List<string>();
            if (HasReferenceTypeConstraint) parts.Add("class");
            if (HasValueTypeConstraint) parts.Add("struct");
            if (HasUnmanagedTypeConstraint) parts.Add("unmanaged");
            if (HasNotNullConstraint) parts.Add("notnull");
            foreach (var constraintType in ConstraintTypes)
                parts.Add(constraintType);
            if (HasConstructorConstraint) parts.Add("new()");

            return parts.Count > 0 ? $"where {Name} : {string.Join(", ", parts)}" : string.Empty;
        }
    }

    /// <summary>
    ///     Returns a string representation of the type parameter constraint.
    /// </summary>
    public override string ToString() => ConstraintSummary;
}

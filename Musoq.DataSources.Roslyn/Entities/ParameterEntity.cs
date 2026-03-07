using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
///     Represents a parameter entity with a type and a name.
/// </summary>
public class ParameterEntity
{
    private readonly SyntaxNode? _methodBody;
    private readonly IParameterSymbol _parameterSymbol;
    private readonly SemanticModel? _semanticModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ParameterEntity" /> class.
    /// </summary>
    /// <param name="parameterSymbol">The parameter symbol representing the parameter.</param>
    public ParameterEntity(IParameterSymbol parameterSymbol)
    {
        _parameterSymbol = parameterSymbol;
        _methodBody = null;
        _semanticModel = null;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ParameterEntity" /> class with method body context.
    /// </summary>
    /// <param name="parameterSymbol">The parameter symbol representing the parameter.</param>
    /// <param name="methodBody">The method body syntax node.</param>
    /// <param name="semanticModel">The semantic model.</param>
    public ParameterEntity(IParameterSymbol parameterSymbol, SyntaxNode? methodBody, SemanticModel? semanticModel)
    {
        _parameterSymbol = parameterSymbol;
        _methodBody = methodBody;
        _semanticModel = semanticModel;
    }

    /// <summary>
    ///     Gets the type of the parameter.
    /// </summary>
    public string Type => _parameterSymbol.Type.Name;

    /// <summary>
    ///     Gets the full type name including namespace.
    /// </summary>
    public string FullTypeName => _parameterSymbol.Type.ToDisplayString();

    /// <summary>
    ///     Gets the ordinal position of the parameter (0-based).
    /// </summary>
    public int Ordinal => _parameterSymbol.Ordinal;

    /// <summary>
    ///     Gets a value indicating whether the parameter has an explicit default value.
    /// </summary>
    public bool HasDefaultValue => _parameterSymbol.HasExplicitDefaultValue;

    /// <summary>
    ///     Gets the default value of the parameter if it has one, null otherwise.
    /// </summary>
    public string? DefaultValue => _parameterSymbol.HasExplicitDefaultValue
        ? _parameterSymbol.ExplicitDefaultValue?.ToString()
        : null;

    /// <summary>
    ///     Gets the name of the parameter.
    /// </summary>
    public string Name => _parameterSymbol.Name;

    /// <summary>
    ///     Gets a value indicating whether the parameter is optional.
    /// </summary>
    public bool IsOptional => _parameterSymbol.IsOptional;

    /// <summary>
    ///     Gets a value indicating whether the parameter is a params array.
    /// </summary>
    public bool IsParams => _parameterSymbol.IsParams;

    /// <summary>
    ///     Gets a value indicating whether the parameter is the 'this' parameter.
    /// </summary>
    public bool IsThis => _parameterSymbol.IsThis;

    /// <summary>
    ///     Gets a value indicating whether the parameter is a discard parameter.
    /// </summary>
    public bool IsDiscard => _parameterSymbol.IsDiscard;

    /// <summary>
    ///     Gets a value indicating whether the parameter is passed by 'in' reference.
    /// </summary>
    public bool IsIn => _parameterSymbol.RefKind == RefKind.In;

    /// <summary>
    ///     Gets a value indicating whether the parameter is passed by 'out' reference.
    /// </summary>
    public bool IsOut => _parameterSymbol.RefKind == RefKind.Out;

    /// <summary>
    ///     Gets a value indicating whether the parameter is passed by 'ref' reference.
    /// </summary>
    public bool IsRef => _parameterSymbol.RefKind == RefKind.Ref;

    /// <summary>
    ///     Gets a value indicating whether the parameter is passed by reference (either 'ref' or 'out').
    /// </summary>
    public bool IsByRef => _parameterSymbol.RefKind is RefKind.Ref or RefKind.Out;

    /// <summary>
    ///     Gets a value indicating whether the parameter is passed by value.
    /// </summary>
    public bool IsByValue => _parameterSymbol.RefKind == RefKind.None;

    /// <summary>
    ///     Gets a value indicating whether the parameter is used within the method body.
    ///     Returns null if the method body context is not available.
    /// </summary>
    public bool? IsUsed
    {
        get
        {
            if (_methodBody == null || _semanticModel == null)
                return null;

            var identifiers = _methodBody.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Where(id => id.Identifier.Text == Name);

            foreach (var identifier in identifiers)
            {
                var symbolInfo = _semanticModel.GetSymbolInfo(identifier);
                if (SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol, _parameterSymbol)) return true;
            }

            return false;
        }
    }

    /// <summary>
    ///     Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        return $"{Type} {Name}";
    }

    /// <summary>
    ///     Gets the attributes applied to the parameter.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<AttributeEntity> Attributes =>
        _parameterSymbol.GetAttributes().Select(attr => new AttributeEntity(attr));
}
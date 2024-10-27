using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents an enumeration entity derived from a type entity.
/// </summary>
public class EnumEntity : TypeEntity
{
    internal readonly SemanticModel SemanticModel;

    internal readonly Solution Solution;
    
    internal new readonly INamedTypeSymbol Symbol;

    internal EnumDeclarationSyntax Syntax { get; }

    /// <summary>
    /// Represents an enumeration entity derived from a type entity.
    /// </summary>
    /// <param name="symbol">The symbol representing the named type.</param>
    /// <param name="syntax">The syntax node of the enumeration.</param>
    /// <param name="semanticModel">The semantic model of the enumeration.</param>
    /// <param name="solution">The solution that contains the enumeration.</param>
    /// <param name="document">The document that contains the enumeration.</param>
    public EnumEntity(INamedTypeSymbol symbol, EnumDeclarationSyntax syntax, SemanticModel semanticModel, Solution solution, DocumentEntity document) 
        : base(symbol)
    {
        Syntax = syntax;
        SemanticModel = semanticModel;
        Solution = solution;
        Document = document;
        Symbol = symbol;
    }
    
    /// <summary>
    /// Gets the document that contains the class.
    /// </summary>
    public DocumentEntity Document { get; }
    
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
    
    /// <summary>
    /// Gets itself.
    /// </summary>
    public EnumEntity Self => this;


    /// <summary>
    /// Gets the properties of the type.
    /// </summary>
    public override IEnumerable<MethodEntity> Methods => [];

    /// <summary>
    /// Gets the properties of the type.
    /// </summary>
    public override IEnumerable<PropertyEntity> Properties => [];
}
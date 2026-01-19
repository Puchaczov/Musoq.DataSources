using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents an interface entity in the Roslyn data source.
/// Inherits from the <see cref="TypeEntity"/> class.
/// </summary>
public class InterfaceEntity : TypeEntity
{
    internal readonly SemanticModel SemanticModel;

    internal readonly Solution Solution;
    
    internal readonly INamedTypeSymbol Symbol;
    
    internal InterfaceDeclarationSyntax Syntax { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InterfaceEntity"/> class.
    /// </summary>
    /// <param name="symbol">The Roslyn named type symbol representing the interface.</param>
    /// <param name="syntax">The syntax node of the interface.</param>
    /// <param name="semanticModel">The semantic model of the interface.</param>
    /// <param name="solution">The solution that contains the interface.</param>
    /// <param name="document">The document that contains the interface.</param>
    public InterfaceEntity(INamedTypeSymbol symbol, InterfaceDeclarationSyntax syntax, SemanticModel semanticModel, Solution solution, DocumentEntity document) 
        : base(symbol)
    {
        Symbol = symbol;
        Syntax = syntax;
        SemanticModel = semanticModel;
        Solution = solution;
        Document = document;
    }
    
    /// <summary>
    /// Gets the document that contains the class.
    /// </summary>
    public DocumentEntity Document { get; }

    /// <summary>
    /// Gets the base interfaces implemented by this interface.
    /// </summary>
    public IEnumerable<string> BaseInterfaces => Symbol.Interfaces.Select(i => i.Name);
    
    /// <summary>
    /// Gets itself.
    /// </summary>
    public InterfaceEntity Self => this;

    /// <summary>
    /// Gets the properties of the type.
    /// </summary>
    public override IEnumerable<MethodEntity> Methods => Syntax.Members
        .OfType<MethodDeclarationSyntax>()
        .Select(m => new MethodEntity(SemanticModel.GetDeclaredSymbol(m)!, m, SemanticModel, Solution));

    /// <summary>
    /// Gets the properties of the type.
    /// </summary>
    public override IEnumerable<PropertyEntity> Properties => Syntax.Members
        .OfType<PropertyDeclarationSyntax>()
        .Select(p => new PropertyEntity(SemanticModel.GetDeclaredSymbol(p)!));

    /// <summary>
    /// Gets the number of references to this interface in the solution.
    /// </summary>
    public int ReferenceCount
    {
        get
        {
            var references = SymbolFinder.FindReferencesAsync(Symbol, Solution).Result;
            return references.Sum(r => r.Locations.Count());
        }
    }

    /// <summary>
    /// Gets a value indicating whether the interface is used (referenced) in the solution.
    /// </summary>
    public bool IsUsed => ReferenceCount > 0;
}
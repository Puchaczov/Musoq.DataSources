using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Musoq.DataSources.Roslyn.Entities
{
    /// <summary>
    /// Represents an interface entity in the Roslyn data source.
    /// Inherits from the <see cref="TypeEntity"/> class.
    /// </summary>
    public class InterfaceEntity : TypeEntity
    {
        internal readonly SemanticModel SemanticModel;

        internal readonly Solution Solution;
    
        internal new readonly INamedTypeSymbol Symbol;
    
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
            .Select(m => new MethodEntity(SemanticModel.GetDeclaredSymbol(m)!, m));

        /// <summary>
        /// Gets the properties of the type.
        /// </summary>
        public override IEnumerable<PropertyEntity> Properties => Syntax.Members
            .OfType<PropertyDeclarationSyntax>()
            .Select(p => new PropertyEntity(SemanticModel.GetDeclaredSymbol(p)!));
    }
}
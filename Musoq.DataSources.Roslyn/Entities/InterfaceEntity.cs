using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
///     Represents an interface entity in the Roslyn data source.
///     Inherits from the <see cref="TypeEntity" /> class.
/// </summary>
public class InterfaceEntity : TypeEntity
{
    internal readonly SemanticModel SemanticModel;

    internal readonly Solution Solution;

    internal readonly INamedTypeSymbol Symbol;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InterfaceEntity" /> class.
    /// </summary>
    /// <param name="symbol">The Roslyn named type symbol representing the interface.</param>
    /// <param name="syntax">The syntax node of the interface.</param>
    /// <param name="semanticModel">The semantic model of the interface.</param>
    /// <param name="solution">The solution that contains the interface.</param>
    /// <param name="document">The document that contains the interface.</param>
    public InterfaceEntity(INamedTypeSymbol symbol, InterfaceDeclarationSyntax syntax, SemanticModel semanticModel,
        Solution solution, DocumentEntity document)
        : base(symbol)
    {
        Symbol = symbol;
        Syntax = syntax;
        SemanticModel = semanticModel;
        Solution = solution;
        Document = document;
    }

    internal InterfaceDeclarationSyntax Syntax { get; }

    /// <summary>
    ///     Gets the document that contains the class.
    /// </summary>
    public DocumentEntity Document { get; }

    /// <summary>
    ///     Gets a value indicating whether this is a partial interface.
    /// </summary>
    public bool IsPartial => Syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));

    /// <summary>
    ///     Gets the base interfaces directly extended by this interface (short names).
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> BaseInterfaces => Symbol.Interfaces.Select(i => i.Name);

    /// <summary>
    ///     Gets all base interfaces transitively, including those inherited through
    ///     interface inheritance chains. Returns fully qualified names.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> AllBaseInterfaces => Symbol.AllInterfaces.Select(i => i.ToDisplayString());

    /// <summary>
    ///     Gets itself.
    /// </summary>
    public InterfaceEntity Self => this;

    /// <summary>
    ///     Gets the properties of the type.
    /// </summary>
    public override IEnumerable<MethodEntity> Methods => Syntax.Members
        .OfType<MethodDeclarationSyntax>()
        .Select(m => new MethodEntity(SemanticModel.GetDeclaredSymbol(m)!, m, SemanticModel, Solution));

    /// <summary>
    ///     Gets the properties of the type.
    /// </summary>
    public override IEnumerable<PropertyEntity> Properties => Syntax.Members
        .OfType<PropertyDeclarationSyntax>()
        .Select(p => new PropertyEntity(SemanticModel.GetDeclaredSymbol(p)!, SemanticModel));

    /// <summary>
    ///     Gets the number of references to this interface in the solution.
    ///     Returns -1 if the operation times out.
    /// </summary>
    public int ReferenceCount
    {
        get
        {
            var references = RoslynAsyncHelper.RunSyncWithTimeout(
                ct => SymbolFinder.FindReferencesAsync(Symbol, Solution, ct)!,
                RoslynAsyncHelper.DefaultReferenceTimeout,
                null);

            return references?.Sum(r => r.Locations.Count()) ?? -1;
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the interface is used (referenced) in the solution.
    /// </summary>
    public bool IsUsed => ReferenceCount > 0;

    /// <summary>
    ///     Gets the attributes applied to the interface.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<AttributeEntity> Attributes =>
        Symbol.GetAttributes().Select(attr => new AttributeEntity(attr));

    /// <summary>
    ///     Gets a value indicating whether the interface has XML documentation.
    /// </summary>
    public bool HasDocumentation
    {
        get
        {
            var trivia = Syntax.GetLeadingTrivia();
            return trivia.Any(t =>
                t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
        }
    }

    /// <summary>
    ///     Gets the type parameters of the interface.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> TypeParameters => Symbol.TypeParameters.Select(tp => tp.Name);

    /// <summary>
    ///     Gets the names of all members defined in this interface.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> MemberNames => Symbol.GetMembers().Select(m => m.Name);

    /// <summary>
    ///     Gets the count of methods declared in this interface.
    /// </summary>
    public int MethodsCount => Syntax.Members.OfType<MethodDeclarationSyntax>().Count();

    /// <summary>
    ///     Gets the count of properties declared in this interface.
    /// </summary>
    public int PropertiesCount => Syntax.Members.OfType<PropertyDeclarationSyntax>().Count();

    /// <summary>
    ///     Gets the events declared in this interface.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<EventEntity> Events
    {
        get
        {
            var events = new List<EventEntity>();

            foreach (var eventDecl in Syntax.Members.OfType<EventDeclarationSyntax>())
            {
                var symbol = SemanticModel.GetDeclaredSymbol(eventDecl);
                if (symbol != null)
                    events.Add(new EventEntity(symbol, Solution, eventDecl));
            }

            foreach (var eventField in Syntax.Members.OfType<EventFieldDeclarationSyntax>())
            foreach (var variable in eventField.Declaration.Variables)
                if (SemanticModel.GetDeclaredSymbol(variable) is IEventSymbol symbol)
                    events.Add(new EventEntity(symbol, Solution, fieldSyntax: eventField));

            return events;
        }
    }

    /// <summary>
    ///     Gets the count of events declared in this interface.
    /// </summary>
    public int EventsCount => Events.Count();
}
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents a struct entity that provides information about a struct in the source code.
/// </summary>
public class StructEntity : TypeEntity
{
    internal readonly SemanticModel SemanticModel;
    internal readonly Solution Solution;
    internal readonly INamedTypeSymbol Symbol;
    internal StructDeclarationSyntax Syntax { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StructEntity"/> class.
    /// </summary>
    /// <param name="symbol">The named type symbol from Roslyn.</param>
    /// <param name="syntax">The syntax node of the struct.</param>
    /// <param name="semanticModel">Semantic model of the struct.</param>
    /// <param name="solution">Solution that contains the struct.</param>
    /// <param name="document">The document that contains the struct.</param>
    public StructEntity(INamedTypeSymbol symbol, StructDeclarationSyntax syntax, SemanticModel semanticModel, Solution solution, DocumentEntity document)
        : base(symbol)
    {
        Syntax = syntax;
        SemanticModel = semanticModel;
        Solution = solution;
        Symbol = symbol;
        Document = document;
    }

    /// <summary>
    /// Gets the document that contains the struct.
    /// </summary>
    public DocumentEntity Document { get; }

    /// <summary>
    /// Gets the text of the struct.
    /// </summary>
    public string Text => Syntax.GetText(Encoding.UTF8).ToString();

    /// <summary>
    /// Gets the lines of code metric for the struct.
    /// </summary>
    public int LinesOfCode
    {
        get
        {
            var lineSpan = Syntax.SyntaxTree.GetLineSpan(Syntax.Span);
            return lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a readonly struct.
    /// </summary>
    public bool IsReadOnly => Syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.ReadOnlyKeyword));

    /// <summary>
    /// Gets a value indicating whether this is a ref struct.
    /// </summary>
    public bool IsRefStruct => Syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.RefKeyword));

    /// <summary>
    /// Gets a value indicating whether this is a record struct.
    /// </summary>
    public bool IsRecordStruct => Symbol.IsRecord;

    /// <summary>
    /// Gets the interfaces implemented by the struct.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> Interfaces => Symbol.Interfaces.Select(i => i.Name);

    /// <summary>
    /// Gets the type parameters of the struct.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> TypeParameters => Symbol.TypeParameters.Select(p => p.Name);

    /// <summary>
    /// Gets the attributes of the struct.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<AttributeEntity> Attributes => Symbol.GetAttributes().Select(a => new AttributeEntity(a));

    /// <summary>
    /// Gets the count of methods in the struct.
    /// </summary>
    public int MethodsCount => Syntax.Members.Count(m => m.Kind() == SyntaxKind.MethodDeclaration);

    /// <summary>
    /// Gets the count of properties in the struct.
    /// </summary>
    public int PropertiesCount => Syntax.Members.Count(m => m.Kind() == SyntaxKind.PropertyDeclaration);

    /// <summary>
    /// Gets the count of fields in the struct.
    /// </summary>
    public int FieldsCount => Syntax.Members.Count(m => m.Kind() == SyntaxKind.FieldDeclaration);

    /// <summary>
    /// Gets the count of constructors in the struct.
    /// </summary>
    public int ConstructorsCount => Syntax.Members.Count(m => m.Kind() == SyntaxKind.ConstructorDeclaration);

    /// <summary>
    /// Gets the count of interfaces implemented by the struct.
    /// </summary>
    public int InterfacesCount => Symbol.Interfaces.Length;

    /// <summary>
    /// Gets the methods of the struct.
    /// </summary>
    public override IEnumerable<MethodEntity> Methods => Syntax.Members
        .OfType<MethodDeclarationSyntax>()
        .Select(m => new MethodEntity(SemanticModel.GetDeclaredSymbol(m)!, m));

    /// <summary>
    /// Gets the properties of the struct.
    /// </summary>
    public override IEnumerable<PropertyEntity> Properties => Syntax.Members
        .OfType<PropertyDeclarationSyntax>()
        .Select(p => new PropertyEntity(SemanticModel.GetDeclaredSymbol(p)!));

    /// <summary>
    /// Gets the fields of the struct.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<FieldEntity> Fields => Syntax.Members
        .OfType<FieldDeclarationSyntax>()
        .SelectMany(f => f.Declaration.Variables
            .Select(v => new FieldEntity(
                (IFieldSymbol)SemanticModel.GetDeclaredSymbol(v)!,
                v)));

    /// <summary>
    /// Gets the constructors of the struct.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<ConstructorEntity> Constructors => Syntax.Members
        .OfType<ConstructorDeclarationSyntax>()
        .Select(c => new ConstructorEntity(SemanticModel.GetDeclaredSymbol(c)!, c));

    /// <summary>
    /// Gets itself.
    /// </summary>
    public StructEntity Self => this;
}

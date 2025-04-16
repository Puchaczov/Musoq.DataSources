using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents a class entity that provides information about a class in the source code.
/// </summary>
public class ClassEntity : TypeEntity
{
    internal readonly SemanticModel SemanticModel;

    internal readonly Solution Solution;
    
    internal readonly INamedTypeSymbol Symbol;
    
    internal ClassDeclarationSyntax Syntax { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassEntity"/> class.
    /// </summary>
    /// <param name="symbol">The named type symbol from Roslyn.</param>
    /// <param name="syntax">The syntax node of the class.</param>
    /// <param name="semanticModel">Semantic model of the class.</param>
    /// <param name="solution">Solution that contains the class.</param>
    /// <param name="document">The document that contains the class.</param>
    public ClassEntity(INamedTypeSymbol symbol, ClassDeclarationSyntax syntax, SemanticModel semanticModel, Solution solution, DocumentEntity document)
        : base(symbol)
    {
        Syntax = syntax;
        SemanticModel = semanticModel;
        Solution = solution;
        Symbol = symbol;
        Document = document;
    }
    
    /// <summary>
    /// Gets the document that contains the class.
    /// </summary>
    public DocumentEntity Document { get; }

    /// <summary>
    /// Gets the text of the class.
    /// </summary>
    public string Text => Syntax.GetText(Encoding.UTF8).ToString();
    
    /// <summary>
    /// Gets the lines of code metric for the class.
    /// </summary>
    public int LinesOfCode
    {
        get
        {
            var lineSpan = Syntax.SyntaxTree
                .GetLineSpan(Syntax.Span);

            var startLine = lineSpan.StartLinePosition.Line;
            var endLine = lineSpan.EndLinePosition.Line;
            var totalLines = endLine - startLine + 1;
            
            return totalLines;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the class is abstract.
    /// </summary>
    public bool IsAbstract => Symbol.IsAbstract;

    /// <summary>
    /// Gets a value indicating whether the class is sealed.
    /// </summary>
    public bool IsSealed => Symbol.IsSealed;
    
    /// <summary>
    /// Gets a value indicating whether the class is static.
    /// </summary>
    public bool IsStatic => Symbol.IsStatic;

    /// <summary>
    /// Gets the base types of the class.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> BaseTypes => Symbol.BaseType != null ? [Symbol.BaseType.Name] : [];

    /// <summary>
    /// Gets the interfaces implemented by the class.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> Interfaces => Symbol.Interfaces.Select(i => i.Name);

    /// <summary>
    /// Gets the type parameters of the class.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> TypeParameters => Symbol.TypeParameters.Select(p => p.Name);

    /// <summary>
    /// Gets the names of the members of the class.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> MemberNames => Symbol.GetMembers().Select(m => m.Name);

    /// <summary>
    /// Gets the attributes of the class.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<AttributeEntity> Attributes => Symbol.GetAttributes().Select(a => new AttributeEntity(a));
    
    /// <summary>
    /// Gets the count of methods in the class.
    /// </summary>
    public int MethodsCount => Syntax.Members.Count(m => m.Kind() == SyntaxKind.MethodDeclaration);

    /// <summary>
    /// Gets the count of properties in the class.
    /// </summary>
    public int PropertiesCount => Syntax.Members.Count(m => m.Kind() == SyntaxKind.PropertyDeclaration);
    
    /// <summary>
    /// Gets the count of fields in the class.
    /// </summary>
    public int FieldsCount => Syntax.Members.Count(m => m.Kind() == SyntaxKind.FieldDeclaration);

    /// <summary>
    /// Gets the inheritance depth of the class.
    /// </summary>
    public int InheritanceDepth
    {
        get
        {
            
            var depth = 0;
            var current = Symbol.BaseType;

            while (current != null)
            {
                depth++;
                current = current.BaseType;
            }

            return depth;
        }
    }
    
    /// <summary>
    /// Gets the count of constructors in the class.
    /// </summary>
    public int ConstructorsCount => Syntax.Members.Count(m => m.Kind() == SyntaxKind.ConstructorDeclaration);
    
    /// <summary>
    /// Gets the count of nested classes in the class.
    /// </summary>
    public int NestedClassesCount => Syntax.Members.Count(m => m.Kind() == SyntaxKind.ClassDeclaration);
    
    /// <summary>
    /// Gets the count of nested interfaces in the class.
    /// </summary>
    public int NestedInterfacesCount => Syntax.Members.Count(m => m.Kind() == SyntaxKind.InterfaceDeclaration);
    
    /// <summary>
    /// Gets the count of interfaces implemented by the class.
    /// </summary>
    public int InterfacesCount => Symbol.Interfaces.Length;

    /// <summary>
    /// Gets the lack of cohesion in methods metric for the class.
    /// </summary>
    public double LackOfCohesion
    {
        get
        {
            var methods = Syntax.Members.OfType<MethodDeclarationSyntax>().ToList();
            var fields = Syntax.Members.OfType<FieldDeclarationSyntax>().ToList();

            if (methods.Count < 2)
            {
                return 0;
            }

            var fieldUsage = new Dictionary<string, HashSet<string>>();

            foreach (var variable in fields.SelectMany(field => field.Declaration.Variables))
            {
                fieldUsage[variable.Identifier.Text] = [];
            }

            foreach (var method in methods)
            {
                var usedFields = GetUsedFields(method, SemanticModel);
                foreach (var field in usedFields.Where(field => fieldUsage.ContainsKey(field)))
                {
                    fieldUsage[field].Add(method.Identifier.Text);
                }
            }

            var disjointFieldUsagePairs = 0;
            var intersectingFieldUsagePairs = 0;

            for (var i = 0; i < methods.Count - 1; i++)
            {
                for (var j = i + 1; j < methods.Count; j++)
                {
                    var method1 = methods[i].Identifier.Text;
                    var method2 = methods[j].Identifier.Text;

                    var hasIntersection = fieldUsage.Values.Any(fieldSet => fieldSet.Contains(method1) && fieldSet.Contains(method2));

                    if (hasIntersection)
                    {
                        intersectingFieldUsagePairs++;
                    }
                    else
                    {
                        disjointFieldUsagePairs++;
                    }
                }
            }

            return disjointFieldUsagePairs > intersectingFieldUsagePairs 
                ? (disjointFieldUsagePairs - intersectingFieldUsagePairs) / (double)methods.Count 
                : 0;
        }
    }
    
    /// <summary>
    /// Gets itself.
    /// </summary>
    public ClassEntity Self => this;

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

    private static HashSet<string> GetUsedFields(MethodDeclarationSyntax method, SemanticModel semanticModel)
    {
        var usedFields = new HashSet<string>();

        foreach (var identifier in method.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            var symbol = ModelExtensions.GetSymbolInfo(semanticModel, identifier).Symbol;
            if (symbol is IFieldSymbol fieldSymbol)
            {
                usedFields.Add(fieldSymbol.Name);
            }
        }

        return usedFields;
    }
}
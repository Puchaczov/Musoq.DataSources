using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
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
        .Select(m => new MethodEntity(SemanticModel.GetDeclaredSymbol(m)!, m, SemanticModel));

    /// <summary>
    /// Gets the properties of the type.
    /// </summary>
    public override IEnumerable<PropertyEntity> Properties => Syntax.Members
        .OfType<PropertyDeclarationSyntax>()
        .Select(p => new PropertyEntity(SemanticModel.GetDeclaredSymbol(p)!));

    /// <summary>
    /// Gets the fields of the class.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<FieldEntity> Fields => Syntax.Members
        .OfType<FieldDeclarationSyntax>()
        .SelectMany(f => f.Declaration.Variables
            .Select(v => new FieldEntity(
                (IFieldSymbol)SemanticModel.GetDeclaredSymbol(v)!,
                v,
                Solution)));

    /// <summary>
    /// Gets the constructors of the class.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<ConstructorEntity> Constructors => Syntax.Members
        .OfType<ConstructorDeclarationSyntax>()
        .Select(c => new ConstructorEntity(SemanticModel.GetDeclaredSymbol(c)!, c));

    /// <summary>
    /// Gets the events of the class.
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
                    events.Add(new EventEntity(symbol, eventDecl));
            }
            
            foreach (var eventField in Syntax.Members.OfType<EventFieldDeclarationSyntax>())
            {
                foreach (var variable in eventField.Declaration.Variables)
                {
                    var symbol = SemanticModel.GetDeclaredSymbol(variable) as IEventSymbol;
                    if (symbol != null)
                        events.Add(new EventEntity(symbol, fieldSyntax: eventField));
                }
            }
            
            return events;
        }
    }

    /// <summary>
    /// Gets the count of events in the class.
    /// </summary>
    public int EventsCount => Events.Count();

    /// <summary>
    /// Gets a value indicating whether the class has XML documentation.
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
    /// Gets the percentage of methods that have XML documentation.
    /// </summary>
    public double MethodDocumentationCoverage
    {
        get
        {
            var methods = Syntax.Members.OfType<MethodDeclarationSyntax>().ToList();
            if (methods.Count == 0)
                return 100.0;

            var documentedCount = methods.Count(m => 
                m.GetLeadingTrivia().Any(t => 
                    t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                    t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)));

            return (documentedCount * 100.0) / methods.Count;
        }
    }

    /// <summary>
    /// Gets the percentage of properties that have XML documentation.
    /// </summary>
    public double PropertyDocumentationCoverage
    {
        get
        {
            var properties = Syntax.Members.OfType<PropertyDeclarationSyntax>().ToList();
            if (properties.Count == 0)
                return 100.0;

            var documentedCount = properties.Count(p => 
                p.GetLeadingTrivia().Any(t => 
                    t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                    t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)));

            return (documentedCount * 100.0) / properties.Count;
        }
    }

    /// <summary>
    /// Gets the afferent coupling (Ca) - number of types that depend on this class.
    /// This is a measure of how many other classes use this class.
    /// Note: This is an approximation based on identifier name matching within cached syntax trees.
    /// For large codebases, consider using FindReferences() for more accurate results.
    /// </summary>
    public int AfferentCoupling
    {
        get
        {
            var dependentTypes = new HashSet<string>();
            
            foreach (var project in Solution.Projects)
            {
                foreach (var document in project.Documents)
                {
                    if (!document.TryGetSyntaxTree(out var tree) || tree == null)
                        continue;
                    
                    var root = tree.GetRoot();
                    var identifiers = root.DescendantNodes().OfType<IdentifierNameSyntax>()
                        .Where(id => id.Identifier.Text == Symbol.Name);
                    
                    foreach (var identifier in identifiers)
                    {
                        var containingType = identifier.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
                        if (containingType != null && containingType != Syntax)
                        {
                            var typeName = containingType.Identifier.Text;
                            if (typeName != Symbol.Name)
                                dependentTypes.Add(typeName);
                        }
                    }
                }
            }
            
            return dependentTypes.Count;
        }
    }

    /// <summary>
    /// Gets the efferent coupling (Ce) - number of types this class depends on.
    /// This is a measure of how many other classes this class uses.
    /// </summary>
    public int EfferentCoupling
    {
        get
        {
            var dependencies = new HashSet<string>();
            
            foreach (var identifier in Syntax.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var symbolInfo = SemanticModel.GetSymbolInfo(identifier);
                var symbol = symbolInfo.Symbol;
                
                if (symbol?.ContainingType != null && 
                    symbol.ContainingType.Name != Symbol.Name &&
                    symbol.ContainingType.TypeKind == TypeKind.Class)
                {
                    dependencies.Add(symbol.ContainingType.Name);
                }
            }
            
            if (Symbol.BaseType != null && Symbol.BaseType.Name != "Object")
            {
                dependencies.Add(Symbol.BaseType.Name);
            }
            
            foreach (var iface in Symbol.Interfaces)
            {
                dependencies.Add(iface.Name);
            }
            
            return dependencies.Count;
        }
    }

    /// <summary>
    /// Gets the instability metric (I = Ce / (Ca + Ce)).
    /// Value ranges from 0 (completely stable) to 1 (completely unstable).
    /// </summary>
    public double Instability
    {
        get
        {
            var ca = AfferentCoupling;
            var ce = EfferentCoupling;
            var total = ca + ce;
            
            if (total == 0)
                return 0;
            
            return (double)ce / total;
        }
    }

    /// <summary>
    /// Gets the Weighted Methods per Class (WMC) metric.
    /// Sum of cyclomatic complexities of all methods.
    /// </summary>
    public int WeightedMethodsPerClass
    {
        get
        {
            return Methods.Sum(m => m.CyclomaticComplexity);
        }
    }

    /// <summary>
    /// Gets the maximum cyclomatic complexity among all methods.
    /// </summary>
    public int MaxMethodComplexity
    {
        get
        {
            var methods = Methods.ToList();
            return methods.Count == 0 ? 0 : methods.Max(m => m.CyclomaticComplexity);
        }
    }

    /// <summary>
    /// Gets the average cyclomatic complexity of methods.
    /// </summary>
    public double AverageMethodComplexity
    {
        get
        {
            var methods = Methods.ToList();
            return methods.Count == 0 ? 0 : methods.Average(m => m.CyclomaticComplexity);
        }
    }

    /// <summary>
    /// Gets the number of references to this class in the solution.
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
    /// Gets a value indicating whether the class is used (referenced) in the solution.
    /// </summary>
    public bool IsUsed => ReferenceCount > 0;

    /// <summary>
    /// Gets the count of unused fields in the class.
    /// </summary>
    public int UnusedFieldCount => Fields.Count(f => f.IsUsed == false);

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
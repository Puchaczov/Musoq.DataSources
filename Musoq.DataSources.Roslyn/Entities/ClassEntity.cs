using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents a class entity that provides information about a class in the source code.
/// </summary>
public class ClassEntity : TypeEntity
{
    private readonly ClassDeclarationSyntax _syntax;
    private readonly SemanticModel _semanticModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassEntity"/> class.
    /// </summary>
    /// <param name="symbol">The named type symbol from Roslyn.</param>
    /// <param name="syntax">The syntax node of the class.</param>
    /// <param name="semanticModel"></param>
    public ClassEntity(INamedTypeSymbol symbol, ClassDeclarationSyntax syntax, SemanticModel semanticModel)
        : base(symbol)
    {
        _syntax = syntax;
        _semanticModel = semanticModel;
    }

    /// <summary>
    /// Gets the text of the class.
    /// </summary>
    public string Text => Symbol.ToDisplayString();

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
    public int MethodsCount => Symbol.GetMembers().Count(m => m.Kind == SymbolKind.Method && 
                                                              ((IMethodSymbol)m).MethodKind != MethodKind.PropertyGet && 
                                                              ((IMethodSymbol)m).MethodKind != MethodKind.PropertySet && 
                                                              ((IMethodSymbol)m).MethodKind != MethodKind.EventAdd && 
                                                              ((IMethodSymbol)m).MethodKind != MethodKind.EventRemove && 
                                                              ((IMethodSymbol)m).MethodKind != MethodKind.EventRaise && 
                                                              ((IMethodSymbol)m).MethodKind != MethodKind.Destructor && 
                                                              ((IMethodSymbol)m).MethodKind != MethodKind.StaticConstructor && 
                                                              ((IMethodSymbol)m).MethodKind != MethodKind.Constructor);

    /// <summary>
    /// Gets the count of properties in the class.
    /// </summary>
    public int PropertiesCount => Symbol.GetMembers().Count(m => m.Kind == SymbolKind.Property);
    
    /// <summary>
    /// Gets the count of fields in the class.
    /// </summary>
    public int FieldsCount => Symbol.GetMembers().Count(m => m.Kind == SymbolKind.Field);

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
    public int ConstructorsCount => Symbol.GetMembers().Count(m => m.Kind == SymbolKind.Method && ((IMethodSymbol)m).MethodKind == MethodKind.Constructor);
    
    /// <summary>
    /// Gets the count of nested classes in the class.
    /// </summary>
    public int NestedClassesCount => Symbol.GetMembers().Count(m => m.Kind == SymbolKind.NamedType);
    
    /// <summary>
    /// Gets the count of nested interfaces in the class.
    /// </summary>
    public int NestedInterfacesCount => Symbol.GetMembers().Count(m => m.Kind == SymbolKind.NamedType && ((INamedTypeSymbol)m).TypeKind == TypeKind.Interface);
    
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
            var methods = _syntax.Members.OfType<MethodDeclarationSyntax>().ToList();
            var fields = _syntax.Members.OfType<FieldDeclarationSyntax>().ToList();

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
                var usedFields = GetUsedFields(method, _semanticModel);
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

    private static HashSet<string> GetUsedFields(MethodDeclarationSyntax method, SemanticModel semanticModel)
    {
        var usedFields = new HashSet<string>();

        foreach (var identifier in method.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
            if (symbol is IFieldSymbol fieldSymbol)
            {
                usedFields.Add(fieldSymbol.Name);
            }
        }

        return usedFields;
    }
}
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
///     Represents a property entity in the Roslyn data source.
/// </summary>
public class PropertyEntity
{
    private readonly PropertyDeclarationSyntax? _propertyDeclaration;
    private readonly IPropertySymbol _propertySymbol;
    private readonly SemanticModel? _semanticModel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PropertyEntity" /> class.
    /// </summary>
    /// <param name="propertySymbol">The property symbol representing the property.</param>
    public PropertyEntity(IPropertySymbol propertySymbol)
    {
        _propertySymbol = propertySymbol;
        _semanticModel = null;

        // Get the syntax node from the symbol
        var syntaxReference = propertySymbol.DeclaringSyntaxReferences.FirstOrDefault();
        _propertyDeclaration = syntaxReference?.GetSyntax() as PropertyDeclarationSyntax;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PropertyEntity" /> class with semantic model.
    /// </summary>
    /// <param name="propertySymbol">The property symbol representing the property.</param>
    /// <param name="semanticModel">The semantic model for analyzing type references in accessor bodies.</param>
    public PropertyEntity(IPropertySymbol propertySymbol, SemanticModel semanticModel)
    {
        _propertySymbol = propertySymbol;
        _semanticModel = semanticModel;

        // Get the syntax node from the symbol
        var syntaxReference = propertySymbol.DeclaringSyntaxReferences.FirstOrDefault();
        _propertyDeclaration = syntaxReference?.GetSyntax() as PropertyDeclarationSyntax;
    }

    /// <summary>
    ///     Gets the name of the property.
    /// </summary>
    public string Name => _propertySymbol.Name;

    /// <summary>
    ///     Gets the type of the property as a display string.
    /// </summary>
    public string Type => _propertySymbol.Type.Name;

    /// <summary>
    ///     Gets the full type name including namespace.
    /// </summary>
    public string FullTypeName => _propertySymbol.Type.ToDisplayString();

    /// <summary>
    ///     Gets a value indicating whether the property is an indexer.
    /// </summary>
    public bool IsIndexer => _propertySymbol.IsIndexer;

    /// <summary>
    ///     Gets a value indicating whether the property is read-only.
    /// </summary>
    public bool IsReadOnly => _propertySymbol.IsReadOnly;

    /// <summary>
    ///     Gets a value indicating whether the property is write-only.
    /// </summary>
    public bool IsWriteOnly => _propertySymbol.IsWriteOnly;

    /// <summary>
    ///     Gets a value indicating whether the property is required.
    /// </summary>
    public bool IsRequired => _propertySymbol.IsRequired;

    /// <summary>
    ///     Gets a value indicating whether the property is associated with events.
    /// </summary>
    public bool IsWithEvents => _propertySymbol.IsWithEvents;

    /// <summary>
    ///     Gets a value indicating whether the property is virtual.
    /// </summary>
    public bool IsVirtual => _propertySymbol.IsVirtual;

    /// <summary>
    ///     Gets a value indicating whether the property is an override.
    /// </summary>
    public bool IsOverride => _propertySymbol.IsOverride;

    /// <summary>
    ///     Gets a value indicating whether the property is abstract.
    /// </summary>
    public bool IsAbstract => _propertySymbol.IsAbstract;

    /// <summary>
    ///     Gets a value indicating whether the property is sealed.
    /// </summary>
    public bool IsSealed => _propertySymbol.IsSealed;

    /// <summary>
    ///     Gets a value indicating whether the property is static.
    /// </summary>
    public bool IsStatic => _propertySymbol.IsStatic;

    /// <summary>
    ///     Gets the modifiers of the property (e.g., public, private, protected).
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> Modifiers => _propertySymbol.DeclaringSyntaxReferences
        .FirstOrDefault()?.GetSyntax()
        .ChildTokens()
        .Where(token => token.IsKind(SyntaxKind.PublicKeyword) ||
                        token.IsKind(SyntaxKind.PrivateKeyword) ||
                        token.IsKind(SyntaxKind.ProtectedKeyword) ||
                        token.IsKind(SyntaxKind.InternalKeyword) ||
                        token.IsKind(SyntaxKind.AbstractKeyword) ||
                        token.IsKind(SyntaxKind.SealedKeyword))
        .Select(token => token.ValueText) ?? [];

    /// <summary>
    ///     Gets a value indicating whether the property is an auto-implemented property.
    ///     Returns true for properties with no explicit getter/setter body.
    /// </summary>
    public bool IsAutoProperty
    {
        get
        {
            if (_propertyDeclaration?.AccessorList == null)
                // Properties without accessor lists (expression-bodied properties, or properties
                // without syntax references) are not auto-properties
                return false;

            foreach (var accessor in _propertyDeclaration.AccessorList.Accessors)
                // If any accessor has a body or expression body, it's not an auto-property
                if (accessor.Body != null || accessor.ExpressionBody != null)
                    return false;

            return true;
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the property has a get accessor.
    /// </summary>
    public bool HasGetter
    {
        get
        {
            if (_propertyDeclaration?.AccessorList != null)
                return _propertyDeclaration.AccessorList.Accessors
                    .Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));

            // Expression-bodied properties have an implicit getter
            if (_propertyDeclaration?.ExpressionBody != null) return true;

            return false;
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the property has a set accessor (includes init).
    /// </summary>
    public bool HasSetter
    {
        get
        {
            if (_propertyDeclaration?.AccessorList == null) return false;

            return _propertyDeclaration.AccessorList.Accessors
                .Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration) ||
                          a.IsKind(SyntaxKind.InitAccessorDeclaration));
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the property has an init accessor specifically.
    /// </summary>
    public bool HasInitSetter
    {
        get
        {
            if (_propertyDeclaration?.AccessorList == null) return false;

            return _propertyDeclaration.AccessorList.Accessors
                .Any(a => a.IsKind(SyntaxKind.InitAccessorDeclaration));
        }
    }

    /// <summary>
    ///     Gets the accessibility of the property (public, private, etc.).
    /// </summary>
    public string Accessibility => _propertySymbol.DeclaredAccessibility.ToString().ToLowerInvariant();

    /// <summary>
    ///     Gets the start line number of the property in the source file (1-based).
    /// </summary>
    public int StartLine
    {
        get
        {
            if (_propertyDeclaration == null) return 0;
            var lineSpan = _propertyDeclaration.SyntaxTree.GetLineSpan(_propertyDeclaration.Span);
            return lineSpan.StartLinePosition.Line + 1;
        }
    }

    /// <summary>
    ///     Gets the end line number of the property in the source file (1-based).
    /// </summary>
    public int EndLine
    {
        get
        {
            if (_propertyDeclaration == null) return 0;
            var lineSpan = _propertyDeclaration.SyntaxTree.GetLineSpan(_propertyDeclaration.Span);
            return lineSpan.EndLinePosition.Line + 1;
        }
    }

    /// <summary>
    ///     Gets the file path of the source file containing this property.
    /// </summary>
    public string? SourceFilePath => _propertyDeclaration?.SyntaxTree.FilePath;

    /// <summary>
    ///     Gets the containing type name of the property.
    /// </summary>
    public string ContainingTypeName => _propertySymbol.ContainingType?.Name ?? string.Empty;

    /// <summary>
    ///     Gets the containing namespace of the property.
    /// </summary>
    public string ContainingNamespace => _propertySymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

    /// <summary>
    ///     Gets a value indicating whether the property has XML documentation.
    /// </summary>
    public bool HasDocumentation
    {
        get
        {
            if (_propertyDeclaration == null) return false;
            var trivia = _propertyDeclaration.GetLeadingTrivia();
            return trivia.Any(t =>
                t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
        }
    }

    /// <summary>
    ///     Gets the default value expression of the property initializer, if any.
    /// </summary>
    public string? DefaultValue => _propertyDeclaration?.Initializer?.Value.ToString();

    /// <summary>
    ///     Gets the attributes applied to the property.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<AttributeEntity> Attributes =>
        _propertySymbol.GetAttributes().Select(attr => new AttributeEntity(attr));

    /// <summary>
    ///     Gets the explicit interface implementations of this property.
    ///     Returns interface names for properties that use explicit interface implementation syntax.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> ExplicitInterfaceImplementations =>
        _propertySymbol.ExplicitInterfaceImplementations.Select(p => p.ContainingType.ToDisplayString());

    /// <summary>
    ///     Gets a value indicating whether this property is an explicit interface implementation.
    /// </summary>
    public bool IsExplicitInterfaceImplementation => _propertySymbol.ExplicitInterfaceImplementations.Length > 0;

    /// <summary>
    ///     Gets the types referenced within property accessor bodies (getters, setters, expression bodies),
    ///     including usage context (casts, is/as operators, pattern matching, local variable types, etc.).
    ///     Returns an empty collection if the semantic model is not available or the property has no accessor bodies.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<TypeReferenceEntity> ReferencedTypes
    {
        get
        {
            if (_semanticModel == null || _propertyDeclaration == null)
                return Enumerable.Empty<TypeReferenceEntity>();

            var bodies = new List<SyntaxNode>();

            // Expression-bodied property: int Foo => expr;
            if (_propertyDeclaration.ExpressionBody != null)
                bodies.Add(_propertyDeclaration.ExpressionBody);

            // Accessor bodies: get { ... } set { ... }
            if (_propertyDeclaration.AccessorList != null)
            {
                foreach (var accessor in _propertyDeclaration.AccessorList.Accessors)
                {
                    if (accessor.Body != null)
                        bodies.Add(accessor.Body);
                    else if (accessor.ExpressionBody != null)
                        bodies.Add(accessor.ExpressionBody);
                }
            }

            if (bodies.Count == 0)
                return Enumerable.Empty<TypeReferenceEntity>();

            var references = new List<TypeReferenceEntity>();
            var tree = _propertyDeclaration.SyntaxTree;

            foreach (var body in bodies)
            {
                TypeReferenceHelper.CollectTypeReferences(references, body, _semanticModel, tree);
            }

            return references;
        }
    }

    /// <summary>
    ///     Gets the local variables declared in property accessor bodies.
    ///     Returns an empty collection if the semantic model is not available or the property has no accessor bodies.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<VariableEntity> LocalVariables
    {
        get
        {
            if (_semanticModel == null || _propertyDeclaration == null)
                return Enumerable.Empty<VariableEntity>();

            var variables = new List<VariableEntity>();

            var accessorBodies = new List<SyntaxNode>();

            if (_propertyDeclaration.AccessorList != null)
            {
                foreach (var accessor in _propertyDeclaration.AccessorList.Accessors)
                {
                    if (accessor.Body != null)
                        accessorBodies.Add(accessor.Body);
                }
            }

            foreach (var body in accessorBodies)
            foreach (var declaration in body.DescendantNodes()
                         .OfType<LocalDeclarationStatementSyntax>())
            foreach (var variable in declaration.Declaration.Variables)
                if (_semanticModel.GetDeclaredSymbol(variable) is ILocalSymbol symbol)
                    variables.Add(new VariableEntity(symbol, variable, _semanticModel, body));

            return variables;
        }
    }

    /// <summary>
    ///     Gets the count of local variables in property accessor bodies.
    /// </summary>
    public int LocalVariableCount => LocalVariables.Count();
}
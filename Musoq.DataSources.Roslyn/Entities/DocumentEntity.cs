using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents a document entity that provides access to various types of declarations
/// within a Roslyn document, such as classes, interfaces, and enums.
/// </summary>
public class DocumentEntity
{
    private readonly Document _document;
    private readonly Solution _solution;
    private SyntaxTree? _syntaxTree;
    private SemanticModel? _semanticModel;
    private bool _wasInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentEntity"/> class.
    /// </summary>
    /// <param name="document">The Roslyn document to be represented by this entity.</param>
    /// <param name="solution">The Roslyn solution that contains the document.</param>
    public DocumentEntity(Document document, Solution solution)
    {
        _document = document;
        _solution = solution;
    }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentEntity"/> class.
    /// </summary>
    /// <param name="document">The Roslyn document to be represented by this entity.</param>
    /// <param name="solution">The Roslyn solution that contains the document.</param>
    /// <param name="syntaxTree">The syntax tree of the document.</param>
    /// <param name="semanticModel">The semantic model of the document.</param>
    protected DocumentEntity(Document document, Solution solution, SyntaxTree syntaxTree, SemanticModel semanticModel)
    {
        _document = document;
        _solution = solution;
        _syntaxTree = syntaxTree;
        _semanticModel = semanticModel;
        _wasInitialized = true;
    }

    /// <summary>
    /// Gets the name of the document.
    /// </summary>
    public string Name => _document.Name;

    /// <summary>
    /// Gets the text content of the document.
    /// </summary>
    public string? Text
    {
        get
        {
            if (_document.TryGetText(out var text))
            {
                return text.ToString();
            }

            return null;
        }
    }

    /// <summary>
    /// Gets the count of class declarations in the document.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the document is not initialized.</exception>
    public int ClassCount
    {
        get
        {
            if (!_wasInitialized)
                throw new InvalidOperationException("Document is not initialized.");

            return Classes.Count();
        }
    }

    /// <summary>
    /// Gets the count of interface declarations in the document.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the document is not initialized.</exception>
    public int InterfaceCount
    {
        get
        {
            if (!_wasInitialized)
                throw new InvalidOperationException("Document is not initialized.");

            return Interfaces.Count();
        }
    }

    /// <summary>
    /// Gets the count of enum declarations in the document.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the document is not initialized.</exception>
    public int EnumCount
    {
        get
        {
            if (!_wasInitialized)
                throw new InvalidOperationException("Document is not initialized.");

            return Enums.Count();
        }
    }

    /// <summary>
    /// Gets the class declarations in the document.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the document is not initialized.</exception>
    public IEnumerable<ClassEntity> Classes
    {
        get
        {
            if (!_wasInitialized)
                throw new InvalidOperationException("Document is not initialized.");

            return GetTypeEntities<ClassDeclarationSyntax, ClassEntity>();
        }
    }

    /// <summary>
    /// Gets the interface declarations in the document.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the document is not initialized.</exception>
    public IEnumerable<InterfaceEntity> Interfaces
    {
        get
        {
            if (!_wasInitialized)
                throw new InvalidOperationException("Document is not initialized.");

            return GetTypeEntities<InterfaceDeclarationSyntax, InterfaceEntity>();
        }
    }

    /// <summary>
    /// Gets the enum declarations in the document.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the document is not initialized.</exception>
    public IEnumerable<EnumEntity> Enums
    {
        get
        {
            if (!_wasInitialized)
                throw new InvalidOperationException("Document is not initialized.");

            return GetTypeEntities<EnumDeclarationSyntax, EnumEntity>();
        }
    }

    /// <summary>
    /// Initializes the document entity by loading the syntax tree and semantic model.
    /// </summary>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    public async Task InitializeAsync()
    {
        _syntaxTree = await _document.GetSyntaxTreeAsync();
        _semanticModel = await _document.GetSemanticModelAsync();
        _wasInitialized = true;
    }

    /// <summary>
    /// Gets the type entities of the specified syntax and entity types.
    /// </summary>
    /// <typeparam name="TSyntax">The type of the syntax node.</typeparam>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <returns>A collection of type entities.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the document is not initialized.</exception>
    protected IEnumerable<TEntity> GetTypeEntities<TSyntax, TEntity>()
        where TSyntax : BaseTypeDeclarationSyntax
        where TEntity : TypeEntity
    {
        if (_syntaxTree is null || _semanticModel is null)
            throw new InvalidOperationException("Document is not initialized.");

        return FilterNodes<TSyntax>(_syntaxTree.GetRoot()).Select(CreateEntity<TEntity>);
    }

    /// <summary>
    /// Filters the nodes of the specified type from the syntax tree root.
    /// </summary>
    /// <typeparam name="T">The type of the syntax node to filter.</typeparam>
    /// <param name="root">The root syntax node.</param>
    /// <returns>A list of filtered syntax nodes.</returns>
    private static List<T> FilterNodes<T>(SyntaxNode root)
    {
        var interfaces = root.DescendantNodes().OfType<T>().ToList();
        return interfaces;
    }

    /// <summary>
    /// Creates an entity of the specified type from the given syntax node.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity to create.</typeparam>
    /// <param name="node">The syntax node representing the type declaration.</param>
    /// <returns>The created entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the semantic model is not initialized or the symbol could not be retrieved.</exception>
    /// <exception cref="ArgumentException">Thrown if the entity type is unsupported.</exception>
    private TEntity CreateEntity<TEntity>(BaseTypeDeclarationSyntax node) where TEntity : TypeEntity
    {
        if (_semanticModel is null)
            throw new InvalidOperationException("Semantic model is not initialized.");

        var symbol = _semanticModel.GetDeclaredSymbol(node);

        if (symbol is null)
            throw new InvalidOperationException("Could not get symbol for type declaration.");

        if (typeof(TEntity) == typeof(ClassEntity))
            return (TEntity)(object)new ClassEntity((INamedTypeSymbol)symbol, (ClassDeclarationSyntax)node, _semanticModel, _solution, this);
        if (typeof(TEntity) == typeof(InterfaceEntity))
            return (TEntity)(object)new InterfaceEntity((INamedTypeSymbol)symbol, (InterfaceDeclarationSyntax)node, _semanticModel, _solution, this);
        if (typeof(TEntity) == typeof(EnumEntity))
            return (TEntity)(object)new EnumEntity((INamedTypeSymbol)symbol, (EnumDeclarationSyntax)node, _semanticModel, _solution, this);

        throw new ArgumentException($"Unsupported entity type: {typeof(TEntity)}");
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    /// Gets the absolute file path of the document on disk.
    /// Returns null for in-memory or generated documents without a physical file.
    /// </summary>
    public string? FilePath => _document.FilePath;

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
    /// Gets the struct declarations in the document.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the document is not initialized.</exception>
    public IEnumerable<StructEntity> Structs
    {
        get
        {
            if (!_wasInitialized)
                throw new InvalidOperationException("Document is not initialized.");

            if (_syntaxTree is null || _semanticModel is null)
                throw new InvalidOperationException("Document is not initialized.");

            return FilterNodes<StructDeclarationSyntax>(_syntaxTree.GetRoot())
                .Select(node =>
                {
                    var symbol = _semanticModel.GetDeclaredSymbol(node);
                    if (symbol is null)
                        throw new InvalidOperationException("Could not get symbol for struct declaration.");
                    return new StructEntity((INamedTypeSymbol)symbol, node, _semanticModel, _solution, this);
                });
        }
    }

    /// <summary>
    /// Gets the count of struct declarations in the document.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the document is not initialized.</exception>
    public int StructCount
    {
        get
        {
            if (!_wasInitialized)
                throw new InvalidOperationException("Document is not initialized.");

            return Structs.Count();
        }
    }

    /// <summary>
    /// Gets the using directives in the document.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the document is not initialized.</exception>
    public IEnumerable<UsingDirectiveEntity> UsingDirectives
    {
        get
        {
            if (!_wasInitialized)
                throw new InvalidOperationException("Document is not initialized.");

            if (_syntaxTree is null)
                throw new InvalidOperationException("Document is not initialized.");

            return _syntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Select(u => new UsingDirectiveEntity(u));
        }
    }

    /// <summary>
    /// Gets the count of using directives in the document.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the document is not initialized.</exception>
    public int UsingDirectiveCount
    {
        get
        {
            if (!_wasInitialized)
                throw new InvalidOperationException("Document is not initialized.");

            return UsingDirectives.Count();
        }
    }

    /// <summary>
    /// Gets the total lines of code in the document.
    /// </summary>
    public int LinesOfCode
    {
        get
        {
            if (_document.TryGetText(out var text))
            {
                return text.Lines.Count;
            }
            return 0;
        }
    }

    /// <summary>
    /// Gets the count of total type declarations (classes, interfaces, enums, structs) in the document.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the document is not initialized.</exception>
    public int TotalTypeCount
    {
        get
        {
            if (!_wasInitialized)
                throw new InvalidOperationException("Document is not initialized.");

            return ClassCount + InterfaceCount + EnumCount + StructCount;
        }
    }

    /// <summary>
    /// Initializes the document entity by loading the syntax tree and semantic model.
    /// </summary>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _syntaxTree = await _document.GetSyntaxTreeAsync(cancellationToken);
        _semanticModel = await _document.GetSemanticModelAsync(cancellationToken);
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
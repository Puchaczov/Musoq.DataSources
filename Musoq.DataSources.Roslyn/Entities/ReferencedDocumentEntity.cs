using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents a referenced document entity that provides access to specific places in the source code
/// </summary>
public class ReferencedDocumentEntity : DocumentEntity
{
    private readonly ReferenceLocation _referenceLocation;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferencedDocumentEntity"/> class.
    /// </summary>
    /// <param name="document">The document that is being referenced.</param>
    /// <param name="solution">The solution that contains the document.</param>
    /// <param name="referenceLocation">The location of the reference.</param>
    /// <param name="syntaxTree">The syntax tree of the document.</param>
    /// <param name="semanticModel">The semantic model of the document</param>
    public ReferencedDocumentEntity(Document document, Solution solution, SyntaxTree syntaxTree, SemanticModel semanticModel, ReferenceLocation referenceLocation) 
        : base(document, solution, syntaxTree, semanticModel)
    {
        _referenceLocation = referenceLocation;
    }
    
    /// <summary>
    /// Gets the line where the reference starts.
    /// </summary>
    public int StartLine => _referenceLocation.Location.GetLineSpan().StartLinePosition.Line;
    
    /// <summary>
    /// Gets the column where the reference starts.
    /// </summary>
    public int StartColumn => _referenceLocation.Location.GetLineSpan().StartLinePosition.Character;
    
    /// <summary>
    /// Gets the line where the reference ends.
    /// </summary>
    public int EndLine => _referenceLocation.Location.GetLineSpan().EndLinePosition.Line;
    
    /// <summary>
    /// Gets the column where the reference ends.
    /// </summary>
    public int EndColumn => _referenceLocation.Location.GetLineSpan().EndLinePosition.Character;

    /// <summary>
    /// Gets the classes that are referenced by the reference location.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<ClassEntity> ReferencedClasses => Classes.Where(f => f.Syntax.FullSpan.Start <= _referenceLocation.Location.SourceSpan.Start && f.Syntax.FullSpan.End >= _referenceLocation.Location.SourceSpan.End);
    
    /// <summary>
    /// Gets the interfaces that are referenced by the reference location.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<InterfaceEntity> ReferencedInterfaces => Interfaces.Where(f => f.Syntax.FullSpan.Start <= _referenceLocation.Location.SourceSpan.Start && f.Syntax.FullSpan.End >= _referenceLocation.Location.SourceSpan.End);
    
    /// <summary>
    /// Gets the enums that are referenced by the reference location.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<EnumEntity> ReferencedEnums => Enums.Where(f => f.Syntax.FullSpan.Start <= _referenceLocation.Location.SourceSpan.Start && f.Syntax.FullSpan.End >= _referenceLocation.Location.SourceSpan.End);
}
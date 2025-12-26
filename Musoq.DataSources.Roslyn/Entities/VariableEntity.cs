using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents a local variable entity that provides information about a local variable in the source code.
/// </summary>
public class VariableEntity
{
    private readonly ILocalSymbol _localSymbol;
    private readonly VariableDeclaratorSyntax _syntax;
    private readonly SemanticModel _semanticModel;
    private readonly SyntaxNode _containingScope;

    /// <summary>
    /// Initializes a new instance of the <see cref="VariableEntity"/> class.
    /// </summary>
    /// <param name="localSymbol">The local symbol representing the variable.</param>
    /// <param name="syntax">The variable declarator syntax node.</param>
    /// <param name="semanticModel">The semantic model.</param>
    /// <param name="containingScope">The scope containing this variable (method body, block, etc.).</param>
    public VariableEntity(ILocalSymbol localSymbol, VariableDeclaratorSyntax syntax, SemanticModel semanticModel, SyntaxNode containingScope)
    {
        _localSymbol = localSymbol;
        _syntax = syntax;
        _semanticModel = semanticModel;
        _containingScope = containingScope;
    }

    /// <summary>
    /// Gets the name of the variable.
    /// </summary>
    public string Name => _localSymbol.Name;

    /// <summary>
    /// Gets the type of the variable.
    /// </summary>
    public string Type => _localSymbol.Type.Name;

    /// <summary>
    /// Gets the full type name including namespace.
    /// </summary>
    public string FullTypeName => _localSymbol.Type.ToDisplayString();

    /// <summary>
    /// Gets a value indicating whether the variable is const.
    /// </summary>
    public bool IsConst => _localSymbol.IsConst;

    /// <summary>
    /// Gets a value indicating whether the variable has a ref modifier.
    /// </summary>
    public bool IsRef => _localSymbol.RefKind == RefKind.Ref;

    /// <summary>
    /// Gets a value indicating whether the variable has a fixed modifier.
    /// </summary>
    public bool IsFixed => _localSymbol.IsFixed;

    /// <summary>
    /// Gets the constant value if the variable is const, null otherwise.
    /// </summary>
    public object? ConstantValue => _localSymbol.HasConstantValue ? _localSymbol.ConstantValue : null;

    /// <summary>
    /// Gets a value indicating whether the variable has an initializer.
    /// </summary>
    public bool HasInitializer => _syntax.Initializer != null;

    /// <summary>
    /// Gets the initializer expression text if present.
    /// </summary>
    public string? InitializerText => _syntax.Initializer?.Value.ToString();

    /// <summary>
    /// Gets a value indicating whether the variable is used after its declaration.
    /// A variable is considered unused if it is never referenced in the code after its declaration.
    /// </summary>
    public bool IsUsed
    {
        get
        {
            var identifiers = _containingScope.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Where(id => id.Identifier.Text == Name);

            foreach (var identifier in identifiers)
            {
                if (identifier.SpanStart <= _syntax.Span.End)
                    continue;

                var symbolInfo = _semanticModel.GetSymbolInfo(identifier);
                if (SymbolEqualityComparer.Default.Equals(symbolInfo.Symbol, _localSymbol))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Gets the line number where the variable is declared.
    /// </summary>
    public int LineNumber
    {
        get
        {
            var lineSpan = _syntax.SyntaxTree.GetLineSpan(_syntax.Span);
            return lineSpan.StartLinePosition.Line + 1;
        }
    }

    /// <summary>
    /// Returns a string representation of the variable entity.
    /// </summary>
    /// <returns>A string representing the variable.</returns>
    public override string ToString()
    {
        return $"{Type} {Name}";
    }
}

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
///     Represents a local function entity that provides information about a local function in the source code.
/// </summary>
public class LocalFunctionEntity
{
    private readonly IMethodSymbol _symbol;
    private readonly LocalFunctionStatementSyntax _syntax;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LocalFunctionEntity" /> class.
    /// </summary>
    /// <param name="symbol">The method symbol representing the local function.</param>
    /// <param name="syntax">The local function statement syntax node.</param>
    public LocalFunctionEntity(IMethodSymbol symbol, LocalFunctionStatementSyntax syntax)
    {
        _symbol = symbol;
        _syntax = syntax;
    }

    /// <summary>
    ///     Gets the name of the local function.
    /// </summary>
    public string Name => _symbol.Name;

    /// <summary>
    ///     Gets the return type of the local function.
    /// </summary>
    public string ReturnType => _symbol.ReturnType.Name;

    /// <summary>
    ///     Gets a value indicating whether the local function is async.
    /// </summary>
    public bool IsAsync => _symbol.IsAsync;

    /// <summary>
    ///     Gets a value indicating whether the local function is static.
    /// </summary>
    public bool IsStatic => _syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));

    /// <summary>
    ///     Gets the number of parameters.
    /// </summary>
    public int ParameterCount => _symbol.Parameters.Length;

    /// <summary>
    ///     Gets the parameters of the local function.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<ParameterEntity> Parameters =>
        _symbol.Parameters.Select(p => new ParameterEntity(p));

    /// <summary>
    ///     Gets a value indicating whether the local function has a body.
    /// </summary>
    public bool HasBody => _syntax.Body != null || _syntax.ExpressionBody != null;

    /// <summary>
    ///     Gets the number of statements in the local function body.
    /// </summary>
    public int StatementsCount => _syntax.Body?.Statements.Count ?? 0;

    /// <summary>
    ///     Gets the lines of code for this local function.
    /// </summary>
    public int LinesOfCode
    {
        get
        {
            var lineSpan = _syntax.SyntaxTree.GetLineSpan(_syntax.Span);
            return lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
        }
    }

    /// <summary>
    ///     Gets the cyclomatic complexity of the local function.
    /// </summary>
    public int CyclomaticComplexity
    {
        get
        {
            var complexity = 1;

            complexity += CountSyntaxKind(_syntax, SyntaxKind.IfStatement);
            complexity += CountSyntaxKind(_syntax, SyntaxKind.ElseClause);
            complexity += CountSyntaxKind(_syntax, SyntaxKind.CasePatternSwitchLabel);
            complexity += CountSyntaxKind(_syntax, SyntaxKind.WhileStatement);
            complexity += CountSyntaxKind(_syntax, SyntaxKind.ForStatement);
            complexity += CountSyntaxKind(_syntax, SyntaxKind.ForEachStatement);
            complexity += CountSyntaxKind(_syntax, SyntaxKind.CatchClause);
            complexity += CountSyntaxKind(_syntax, SyntaxKind.ConditionalExpression);
            complexity += CountSyntaxKind(_syntax, SyntaxKind.LogicalAndExpression);
            complexity += CountSyntaxKind(_syntax, SyntaxKind.LogicalOrExpression);

            return complexity;
        }
    }

    /// <summary>
    ///     Gets the body text of the local function.
    /// </summary>
    public string Text => _syntax.ToFullString();

    /// <summary>
    ///     Gets the attributes applied to the local function.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<AttributeEntity> Attributes =>
        _symbol.GetAttributes().Select(attr => new AttributeEntity(attr));

    /// <summary>
    ///     Returns a string representation of the local function.
    /// </summary>
    /// <returns>A string representing the local function.</returns>
    public override string ToString()
    {
        var parameters = string.Join(", ", Parameters.Select(p => $"{p.Type} {p.Name}"));
        return $"{ReturnType} {Name}({parameters})";
    }

    private static int CountSyntaxKind(SyntaxNode node, SyntaxKind kind)
    {
        return node.DescendantNodes().Count(n => n.IsKind(kind));
    }
}
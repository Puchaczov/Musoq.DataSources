using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Musoq.Plugins.Attributes;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents a method entity extracted from a Roslyn IMethodSymbol.
/// </summary>
public class MethodEntity
{
    private readonly IMethodSymbol _methodSymbol;
    private readonly MethodDeclarationSyntax _methodDeclaration;

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodEntity"/> class.
    /// </summary>
    /// <param name="methodSymbol">The method symbol to extract information from.</param>
    /// <param name="methodDeclaration">The method declaration syntax node.</param>
    public MethodEntity(IMethodSymbol methodSymbol, MethodDeclarationSyntax methodDeclaration)
    {
        _methodSymbol = methodSymbol;
        _methodDeclaration = methodDeclaration;
    }

    /// <summary>
    /// Gets the name of the method.
    /// </summary>
    public string Name => _methodSymbol.Name;

    /// <summary>
    /// Gets the return type of the method as a string.
    /// </summary>
    public string ReturnType => _methodSymbol.ReturnType.Name;

    /// <summary>
    /// Gets the parameters of the method as a collection of strings.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<ParameterEntity> Parameters => _methodSymbol.Parameters.Select(p => new ParameterEntity(p));

    /// <summary>
    /// Gets the modifiers of the method as a collection of strings.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> Modifiers => _methodSymbol.DeclaringSyntaxReferences
        .FirstOrDefault()?.GetSyntax()
        .ChildTokens()
        .Where(token => token.IsKind(SyntaxKind.PublicKeyword) ||
                        token.IsKind(SyntaxKind.PrivateKeyword) ||
                        token.IsKind(SyntaxKind.ProtectedKeyword) ||
                        token.IsKind(SyntaxKind.InternalKeyword) || 
                        token.IsKind(SyntaxKind.VirtualKeyword) ||
                        token.IsKind(SyntaxKind.OverrideKeyword) ||
                        token.IsKind(SyntaxKind.AbstractKeyword) ||
                        token.IsKind(SyntaxKind.StaticKeyword))
        .Select(token => token.ValueText) ?? new List<string>();

    /// <summary>
    /// Gets the body of the method as a string.
    /// </summary>
    public string Body
    {
        get
        {
            var syntaxReference = _methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            var methodDeclaration = syntaxReference?.GetSyntax() as MethodDeclarationSyntax;
            return methodDeclaration?.Body == null ? string.Empty : methodDeclaration.Body.ToString();
        }
    }

    /// <summary>
    /// Gets the attributes of the method as a collection of <see cref="AttributeEntity"/>.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<AttributeEntity> Attributes
    {
        get
        {
            return _methodSymbol.GetAttributes().Select(attr => new AttributeEntity(attr));
        }
    }
    
    /// <summary>
    /// Gets the cyclomatic complexity of the method.
    /// </summary>
    public int CyclomaticComplexity
    {
        get
        {
            // Start with 1 for the method entry point
            var complexity = 1;

            // Count branching statements
            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.IfStatement);
            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.ElseClause);
            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.CasePatternSwitchLabel);
            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.WhileStatement);
            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.ForStatement);
            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.ForEachStatement);
            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.CatchClause);
            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.ConditionalExpression);

            // Count logical operators
            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.LogicalAndExpression);
            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.LogicalOrExpression);

            return complexity;
        }
    }

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        var modifiers = string.Join(" ", Modifiers);
        var parameters = string.Join(", ", Parameters);
        return $"{modifiers} {ReturnType} {Name}({parameters})";
    }
    
    private static int CountSyntaxKind(SyntaxNode node, SyntaxKind kind)
    {
        return node.DescendantNodes().Count(n => n.IsKind(kind));
    }
}
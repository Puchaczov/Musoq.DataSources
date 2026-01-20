using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Musoq.Plugins.Attributes;
using Musoq.DataSources.Roslyn;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents a method entity extracted from a Roslyn IMethodSymbol.
/// </summary>
public class MethodEntity
{
    private readonly IMethodSymbol _methodSymbol;
    private readonly MethodDeclarationSyntax _methodDeclaration;
    private readonly SemanticModel? _semanticModel;
    private readonly Solution? _solution;

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodEntity"/> class.
    /// </summary>
    /// <param name="methodSymbol">The method symbol to extract information from.</param>
    /// <param name="methodDeclaration">The method declaration syntax node.</param>
    public MethodEntity(IMethodSymbol methodSymbol, MethodDeclarationSyntax methodDeclaration)
    {
        _methodSymbol = methodSymbol;
        _methodDeclaration = methodDeclaration;
        _semanticModel = null;
        _solution = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodEntity"/> class with semantic model.
    /// </summary>
    /// <param name="methodSymbol">The method symbol to extract information from.</param>
    /// <param name="methodDeclaration">The method declaration syntax node.</param>
    /// <param name="semanticModel">The semantic model for analyzing usage.</param>
    public MethodEntity(IMethodSymbol methodSymbol, MethodDeclarationSyntax methodDeclaration, SemanticModel semanticModel)
    {
        _methodSymbol = methodSymbol;
        _methodDeclaration = methodDeclaration;
        _semanticModel = semanticModel;
        _solution = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodEntity"/> class with semantic model and solution context.
    /// </summary>
    /// <param name="methodSymbol">The method symbol to extract information from.</param>
    /// <param name="methodDeclaration">The method declaration syntax node.</param>
    /// <param name="semanticModel">The semantic model for analyzing usage.</param>
    /// <param name="solution">The solution for finding references.</param>
    public MethodEntity(IMethodSymbol methodSymbol, MethodDeclarationSyntax methodDeclaration, SemanticModel semanticModel, Solution solution)
    {
        _methodSymbol = methodSymbol;
        _methodDeclaration = methodDeclaration;
        _semanticModel = semanticModel;
        _solution = solution;
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
    /// Gets the parameters of the method as a collection of ParameterEntity objects.
    /// When semantic model is available, each parameter includes IsUsed property.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<ParameterEntity> Parameters
    {
        get
        {
            var methodBody = (SyntaxNode?)_methodDeclaration.Body ?? _methodDeclaration.ExpressionBody;
            return _methodSymbol.Parameters.Select(p => new ParameterEntity(p, methodBody, _semanticModel));
        }
    }

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
    public string Text
    {
        get
        {
            var syntaxReference = _methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            var methodDeclaration = syntaxReference?.GetSyntax() as MethodDeclarationSyntax;
            if (methodDeclaration == null)
                return string.Empty;

            var stripped = methodDeclaration.WithoutLeadingTrivia();

            if (stripped.Body == null && stripped.ExpressionBody == null)
                return string.Empty;

            return stripped.ToFullString();
        }
    }
    
    /// <summary>
    /// Gets the lines of code metric for the class.
    /// </summary>
    public int LinesOfCode
    {
        get
        {
            var lineSpan = _methodDeclaration.SyntaxTree
                .GetLineSpan(_methodDeclaration.Span);

            var startLine = lineSpan.StartLinePosition.Line;
            var endLine = lineSpan.EndLinePosition.Line;
            var totalLines = endLine - startLine + 1;
            
            return totalLines;
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
            var complexity = 1;

            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.IfStatement);
            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.ElseClause);
            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.CasePatternSwitchLabel);
            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.WhileStatement);
            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.ForStatement);
            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.ForEachStatement);
            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.CatchClause);
            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.ConditionalExpression);

            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.LogicalAndExpression);
            complexity += CountSyntaxKind(_methodDeclaration, SyntaxKind.LogicalOrExpression);

            return complexity;
        }
    }

    /// <summary>
    /// Gets the number of statements in the method body.
    /// Returns 0 for methods without bodies (abstract, interface, extern).
    /// </summary>
    public int StatementsCount
    {
        get
        {
            if (_methodDeclaration.Body != null)
            {
                return _methodDeclaration.Body.Statements.Count;
            }
            
            return 0;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the method has an implementation body.
    /// Returns false for abstract methods, interface method declarations, extern methods,
    /// and partial method declarations without implementation.
    /// </summary>
    public bool HasBody => _methodDeclaration.Body != null || _methodDeclaration.ExpressionBody != null;

    /// <summary>
    /// Gets a value indicating whether the method has a body but contains no statements.
    /// Useful for detecting stub implementations.
    /// </summary>
    public bool IsEmpty => HasBody && _methodDeclaration.Body != null && _methodDeclaration.Body.Statements.Count == 0;

    /// <summary>
    /// Gets a value indicating whether the method body exists but contains only comments and/or whitespace.
    /// Returns false for methods without bodies or expression-bodied members.
    /// </summary>
    public bool BodyContainsOnlyTrivia
    {
        get
        {
            if (_methodDeclaration.Body == null)
            {
                return false;
            }

            if (_methodDeclaration.Body.Statements.Count > 0)
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the method is async.
    /// </summary>
    public bool IsAsync => _methodSymbol.IsAsync;

    /// <summary>
    /// Gets a value indicating whether the method is static.
    /// </summary>
    public bool IsStatic => _methodSymbol.IsStatic;

    /// <summary>
    /// Gets a value indicating whether the method is virtual.
    /// </summary>
    public bool IsVirtual => _methodSymbol.IsVirtual;

    /// <summary>
    /// Gets a value indicating whether the method is abstract.
    /// </summary>
    public bool IsAbstract => _methodSymbol.IsAbstract;

    /// <summary>
    /// Gets a value indicating whether the method is override.
    /// </summary>
    public bool IsOverride => _methodSymbol.IsOverride;

    /// <summary>
    /// Gets a value indicating whether the method is sealed.
    /// </summary>
    public bool IsSealed => _methodSymbol.IsSealed;

    /// <summary>
    /// Gets a value indicating whether the method is an extension method.
    /// </summary>
    public bool IsExtensionMethod => _methodSymbol.IsExtensionMethod;

    /// <summary>
    /// Gets a value indicating whether the method is generic.
    /// </summary>
    public bool IsGeneric => _methodSymbol.IsGenericMethod;

    /// <summary>
    /// Gets the number of type parameters if the method is generic.
    /// </summary>
    public int TypeParameterCount => _methodSymbol.TypeParameters.Length;

    /// <summary>
    /// Gets the type parameters of the method as a collection of strings.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> TypeParameters => _methodSymbol.TypeParameters.Select(tp => tp.Name);

    /// <summary>
    /// Gets the accessibility of the method (public, private, etc.).
    /// </summary>
    public string Accessibility => _methodSymbol.DeclaredAccessibility.ToString().ToLowerInvariant();

    /// <summary>
    /// Gets the number of local functions defined within this method.
    /// </summary>
    public int LocalFunctionCount => _methodDeclaration.DescendantNodes()
        .OfType<LocalFunctionStatementSyntax>()
        .Count();

    /// <summary>
    /// Gets a value indicating whether the method contains any await expressions.
    /// </summary>
    public bool ContainsAwait => _methodDeclaration.DescendantNodes()
        .Any(n => n.IsKind(SyntaxKind.AwaitExpression));

    /// <summary>
    /// Gets the number of await expressions in the method.
    /// </summary>
    public int AwaitCount => _methodDeclaration.DescendantNodes()
        .Count(n => n.IsKind(SyntaxKind.AwaitExpression));

    /// <summary>
    /// Gets a value indicating whether the method contains any lambda expressions.
    /// </summary>
    public bool ContainsLambda => _methodDeclaration.DescendantNodes()
        .Any(n => n.IsKind(SyntaxKind.SimpleLambdaExpression) || 
                  n.IsKind(SyntaxKind.ParenthesizedLambdaExpression));

    /// <summary>
    /// Gets the number of lambda expressions in the method.
    /// </summary>
    public int LambdaCount => _methodDeclaration.DescendantNodes()
        .Count(n => n.IsKind(SyntaxKind.SimpleLambdaExpression) || 
                    n.IsKind(SyntaxKind.ParenthesizedLambdaExpression));

    /// <summary>
    /// Gets a value indicating whether the method has XML documentation.
    /// </summary>
    public bool HasDocumentation
    {
        get
        {
            var trivia = _methodDeclaration.GetLeadingTrivia();
            return trivia.Any(t => 
                t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
        }
    }

    /// <summary>
    /// Gets the nesting depth of the method (max depth of nested control structures).
    /// </summary>
    public int MaxNestingDepth
    {
        get
        {
            if (_methodDeclaration.Body == null)
                return 0;

            return CalculateMaxNestingDepth(_methodDeclaration.Body, 0);
        }
    }

    /// <summary>
    /// Gets the number of return statements in the method.
    /// </summary>
    public int ReturnCount => _methodDeclaration.DescendantNodes()
        .Count(n => n.IsKind(SyntaxKind.ReturnStatement));

    /// <summary>
    /// Gets the number of throw statements in the method.
    /// </summary>
    public int ThrowCount => _methodDeclaration.DescendantNodes()
        .Count(n => n.IsKind(SyntaxKind.ThrowStatement) || n.IsKind(SyntaxKind.ThrowExpression));

    /// <summary>
    /// Gets the number of try-catch blocks in the method.
    /// </summary>
    public int TryCatchCount => _methodDeclaration.DescendantNodes()
        .Count(n => n.IsKind(SyntaxKind.TryStatement));

    /// <summary>
    /// Gets the local variables declared in the method.
    /// Returns an empty collection if the semantic model is not available.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<VariableEntity> LocalVariables
    {
        get
        {
            if (_semanticModel == null || _methodDeclaration.Body == null)
                return Enumerable.Empty<VariableEntity>();

            var variables = new List<VariableEntity>();

            foreach (var declaration in _methodDeclaration.Body.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
            {
                foreach (var variable in declaration.Declaration.Variables)
                {
                    var symbol = _semanticModel.GetDeclaredSymbol(variable) as ILocalSymbol;
                    if (symbol != null)
                    {
                        variables.Add(new VariableEntity(symbol, variable, _semanticModel, _methodDeclaration.Body));
                    }
                }
            }

            return variables;
        }
    }

    /// <summary>
    /// Gets the count of local variables in the method.
    /// </summary>
    public int LocalVariableCount
    {
        get
        {
            if (_methodDeclaration.Body == null)
                return 0;

            return _methodDeclaration.Body.DescendantNodes()
                .OfType<LocalDeclarationStatementSyntax>()
                .SelectMany(d => d.Declaration.Variables)
                .Count();
        }
    }

    /// <summary>
    /// Gets the count of unused parameters in the method.
    /// Returns null if the semantic model is not available.
    /// </summary>
    public int? UnusedParameterCount
    {
        get
        {
            if (_semanticModel == null)
                return null;

            return Parameters.Count(p => p.IsUsed == false);
        }
    }

    /// <summary>
    /// Gets the count of unused local variables in the method.
    /// Returns null if the semantic model is not available.
    /// </summary>
    public int? UnusedVariableCount
    {
        get
        {
            if (_semanticModel == null)
                return null;

            return LocalVariables.Count(v => !v.IsUsed);
        }
    }

    /// <summary>
    /// Gets the number of references to this method in the solution.
    /// Returns null if the solution context is not available.
    /// </summary>
    public int? ReferenceCount
    {
        get
        {
            if (_solution == null)
                return null;

            var references = RoslynAsyncHelper.RunSync(SymbolFinder.FindReferencesAsync(_methodSymbol, _solution));
            return references.Sum(r => r.Locations.Count());
        }
    }

    /// <summary>
    /// Gets a value indicating whether the method is used (referenced) in the solution.
    /// Returns null if the solution context is not available.
    /// </summary>
    public bool? IsUsed
    {
        get
        {
            if (_solution == null)
                return null;

            var refCount = ReferenceCount;
            return refCount > 0;
        }
    }

    /// <summary>
    /// Gets the methods that are called by this method.
    /// Returns an empty collection if the semantic model is not available.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<CalledMethodInfo> Callees
    {
        get
        {
            if (_semanticModel == null)
                return Enumerable.Empty<CalledMethodInfo>();

            var invocations = _methodDeclaration.DescendantNodes()
                .OfType<InvocationExpressionSyntax>();

            var callees = new List<CalledMethodInfo>();
            foreach (var invocation in invocations)
            {
                var symbolInfo = _semanticModel.GetSymbolInfo(invocation);
                var calledMethod = symbolInfo.Symbol as IMethodSymbol ??
                                   symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

                if (calledMethod != null)
                {
                    callees.Add(new CalledMethodInfo(
                        calledMethod.Name,
                        calledMethod.ContainingType?.Name ?? string.Empty,
                        calledMethod.ContainingNamespace?.ToDisplayString() ?? string.Empty,
                        calledMethod.IsStatic,
                        calledMethod.IsExtensionMethod
                    ));
                }
            }
            return callees;
        }
    }

    /// <summary>
    /// Gets the number of methods called by this method.
    /// </summary>
    public int CalleeCount => Callees.Count();

    /// <summary>
    /// Gets a value indicating whether the method calls itself (directly recursive).
    /// </summary>
    public bool IsRecursive
    {
        get
        {
            if (_semanticModel == null)
                return false;

            var thisMethodQualifiedName = $"{_methodSymbol.ContainingType?.ToDisplayString()}.{_methodSymbol.Name}";

            var invocations = _methodDeclaration.DescendantNodes()
                .OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                var symbolInfo = _semanticModel.GetSymbolInfo(invocation);
                var calledMethod = symbolInfo.Symbol as IMethodSymbol ??
                                   symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

                if (calledMethod != null)
                {
                    var calledMethodQualifiedName = $"{calledMethod.ContainingType?.ToDisplayString()}.{calledMethod.Name}";
                    if (calledMethodQualifiedName == thisMethodQualifiedName)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the method returns Task or Task{T}.
    /// </summary>
    public bool ReturnsTask
    {
        get
        {
            var returnTypeName = _methodSymbol.ReturnType.Name;
            return returnTypeName == "Task" || returnTypeName == "ValueTask" ||
                   _methodSymbol.ReturnType.OriginalDefinition.ToDisplayString().StartsWith("System.Threading.Tasks.Task") ||
                   _methodSymbol.ReturnType.OriginalDefinition.ToDisplayString().StartsWith("System.Threading.Tasks.ValueTask");
        }
    }

    /// <summary>
    /// Gets the full return type name including namespace and generic arguments.
    /// </summary>
    public string FullReturnType => _methodSymbol.ReturnType.ToDisplayString();

    /// <summary>
    /// Gets a value indicating whether the return type is nullable.
    /// </summary>
    public bool IsReturnTypeNullable => _methodSymbol.ReturnType.NullableAnnotation == NullableAnnotation.Annotated;

    /// <summary>
    /// Gets the method that this method overrides, if any.
    /// Returns null if this method does not override another method.
    /// </summary>
    public string? OverriddenMethodName => _methodSymbol.OverriddenMethod?.Name;

    /// <summary>
    /// Gets the containing type name of the overridden method, if any.
    /// </summary>
    public string? OverriddenMethodContainingType => _methodSymbol.OverriddenMethod?.ContainingType?.Name;

    /// <summary>
    /// Gets the interface methods that this method implements.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<ImplementedInterfaceMethodInfo> ImplementedInterfaceMethods
    {
        get
        {
            var containingType = _methodSymbol.ContainingType;
            if (containingType == null)
                return Enumerable.Empty<ImplementedInterfaceMethodInfo>();

            var implementedMethods = new List<ImplementedInterfaceMethodInfo>();
            
            foreach (var iface in containingType.AllInterfaces)
            {
                foreach (var member in iface.GetMembers().OfType<IMethodSymbol>())
                {
                    var implementation = containingType.FindImplementationForInterfaceMember(member);
                    if (SymbolEqualityComparer.Default.Equals(implementation, _methodSymbol))
                    {
                        implementedMethods.Add(new ImplementedInterfaceMethodInfo(
                            member.Name,
                            iface.Name,
                            iface.ContainingNamespace?.ToDisplayString() ?? string.Empty
                        ));
                    }
                }
            }
            
            return implementedMethods;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this method implements an interface method.
    /// </summary>
    public bool ImplementsInterface => ImplementedInterfaceMethods.Any();

    /// <summary>
    /// Gets a value indicating whether this method is part of the public API (public or protected in a public type).
    /// </summary>
    public bool IsPublicApi
    {
        get
        {
            if (_methodSymbol.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Public &&
                _methodSymbol.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Protected)
                return false;

            var containingType = _methodSymbol.ContainingType;
            while (containingType != null)
            {
                if (containingType.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Public)
                    return false;
                containingType = containingType.ContainingType;
            }
            return true;
        }
    }

    /// <summary>
    /// Gets the start line number of the method in the source file (1-based).
    /// </summary>
    public int StartLine
    {
        get
        {
            var lineSpan = _methodDeclaration.SyntaxTree.GetLineSpan(_methodDeclaration.Span);
            return lineSpan.StartLinePosition.Line + 1;
        }
    }

    /// <summary>
    /// Gets the end line number of the method in the source file (1-based).
    /// </summary>
    public int EndLine
    {
        get
        {
            var lineSpan = _methodDeclaration.SyntaxTree.GetLineSpan(_methodDeclaration.Span);
            return lineSpan.EndLinePosition.Line + 1;
        }
    }

    /// <summary>
    /// Gets the file path of the source file containing this method.
    /// </summary>
    public string? SourceFilePath => _methodDeclaration.SyntaxTree.FilePath;

    /// <summary>
    /// Gets the containing type name.
    /// </summary>
    public string ContainingTypeName => _methodSymbol.ContainingType?.Name ?? string.Empty;

    /// <summary>
    /// Gets the containing namespace.
    /// </summary>
    public string ContainingNamespace => _methodSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

    /// <summary>
    /// Gets the underlying method symbol for advanced scenarios.
    /// </summary>
    internal IMethodSymbol Symbol => _methodSymbol;

    /// <summary>
    /// Gets the solution context, if available.
    /// </summary>
    internal Solution? Solution => _solution;

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

    private static int CalculateMaxNestingDepth(SyntaxNode node, int currentDepth)
    {
        var maxDepth = currentDepth;
        
        foreach (var child in node.ChildNodes())
        {
            var childDepth = currentDepth;
            
            if (child.IsKind(SyntaxKind.IfStatement) ||
                child.IsKind(SyntaxKind.ElseClause) ||
                child.IsKind(SyntaxKind.WhileStatement) ||
                child.IsKind(SyntaxKind.ForStatement) ||
                child.IsKind(SyntaxKind.ForEachStatement) ||
                child.IsKind(SyntaxKind.DoStatement) ||
                child.IsKind(SyntaxKind.SwitchStatement) ||
                child.IsKind(SyntaxKind.TryStatement) ||
                child.IsKind(SyntaxKind.CatchClause) ||
                child.IsKind(SyntaxKind.FinallyClause) ||
                child.IsKind(SyntaxKind.UsingStatement) ||
                child.IsKind(SyntaxKind.LockStatement))
            {
                childDepth = currentDepth + 1;
            }
            
            var descendantMaxDepth = CalculateMaxNestingDepth(child, childDepth);
            maxDepth = Math.Max(maxDepth, descendantMaxDepth);
        }
        
        return maxDepth;
    }
}
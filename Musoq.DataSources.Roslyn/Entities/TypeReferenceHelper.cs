using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
///     Provides helper methods for collecting type references from syntax nodes.
///     Used by MethodEntity, ConstructorEntity, and PropertyEntity to detect
///     how types are used within code bodies.
/// </summary>
internal static class TypeReferenceHelper
{
    /// <summary>
    ///     Collects type references from all descendant nodes of the given syntax node.
    /// </summary>
    /// <param name="references">The list to add found references to.</param>
    /// <param name="body">The syntax node to walk (method body, accessor body, etc.).</param>
    /// <param name="semanticModel">The semantic model for resolving types.</param>
    /// <param name="tree">The syntax tree for line number resolution.</param>
    public static void CollectTypeReferences(List<TypeReferenceEntity> references, SyntaxNode body,
        SemanticModel semanticModel, SyntaxTree tree)
    {
        foreach (var node in body.DescendantNodes())
        {
            switch (node)
            {
                case CastExpressionSyntax cast:
                    AddTypeReference(references, cast.Type, "Cast", tree, semanticModel);
                    break;

                case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression } isExpr:
                    if (isExpr.Right is TypeSyntax isType)
                        AddTypeReference(references, isType, "Is", tree, semanticModel);
                    break;

                case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression } asExpr:
                    if (asExpr.Right is TypeSyntax asType)
                        AddTypeReference(references, asType, "As", tree, semanticModel);
                    break;

                case IsPatternExpressionSyntax isPattern:
                    CollectPatternTypeReferences(references, isPattern.Pattern, tree, semanticModel);
                    break;

                case SwitchExpressionArmSyntax switchArm:
                    CollectPatternTypeReferences(references, switchArm.Pattern, tree, semanticModel);
                    break;

                case CasePatternSwitchLabelSyntax casePattern:
                    CollectPatternTypeReferences(references, casePattern.Pattern, tree, semanticModel);
                    break;

                case LocalDeclarationStatementSyntax localDecl:
                    if (localDecl.Declaration.Type.IsVar)
                    {
                        // Resolve the actual type behind 'var'
                        foreach (var variable in localDecl.Declaration.Variables)
                        {
                            if (variable.Initializer?.Value == null) continue;

                            var typeInfo = semanticModel.GetTypeInfo(variable.Initializer.Value);
                            var typeSymbol = typeInfo.Type;

                            if (typeSymbol is INamedTypeSymbol namedType && namedType.SpecialType == SpecialType.None)
                            {
                                var kind = GetTypeKindString(namedType);
                                var lineSpan = tree.GetLineSpan(localDecl.Span);
                                var lineNumber = lineSpan.StartLinePosition.Line + 1;

                                references.Add(new TypeReferenceEntity(
                                    namedType.Name,
                                    namedType.ContainingNamespace?.ToDisplayString() ?? string.Empty,
                                    kind,
                                    "LocalVariable",
                                    lineNumber));
                            }
                        }
                    }
                    else
                    {
                        AddTypeReference(references, localDecl.Declaration.Type, "LocalVariable", tree,
                            semanticModel);
                    }

                    break;

                case ObjectCreationExpressionSyntax objCreation:
                    AddTypeReference(references, objCreation.Type, "ObjectCreation", tree, semanticModel);
                    break;

                case TypeOfExpressionSyntax typeOfExpr:
                    AddTypeReference(references, typeOfExpr.Type, "TypeOf", tree, semanticModel);
                    break;

                case DefaultExpressionSyntax defaultExpr:
                    AddTypeReference(references, defaultExpr.Type, "Default", tree, semanticModel);
                    break;

                case ArrayCreationExpressionSyntax arrayCreation:
                    AddTypeReference(references, arrayCreation.Type.ElementType, "ArrayCreation", tree,
                        semanticModel);
                    break;

                case CatchDeclarationSyntax catchDecl:
                    AddTypeReference(references, catchDecl.Type, "CatchDeclaration", tree, semanticModel);
                    break;

                case GenericNameSyntax genericName:
                    foreach (var typeArg in genericName.TypeArgumentList.Arguments)
                        AddTypeReference(references, typeArg, "GenericArgument", tree, semanticModel);
                    break;
            }
        }
    }

    /// <summary>
    ///     Resolves a type syntax node to a TypeReferenceEntity and adds it to the list.
    /// </summary>
    public static void AddTypeReference(List<TypeReferenceEntity> references, TypeSyntax typeSyntax, string usageKind,
        SyntaxTree tree, SemanticModel semanticModel)
    {
        var typeInfo = semanticModel.GetTypeInfo(typeSyntax);
        var typeSymbol = typeInfo.Type ?? semanticModel.GetSymbolInfo(typeSyntax).Symbol as ITypeSymbol;

        if (typeSymbol is not INamedTypeSymbol namedType)
            return;

        if (namedType.SpecialType != SpecialType.None)
            return;

        var kind = GetTypeKindString(namedType);

        var lineSpan = tree.GetLineSpan(typeSyntax.Span);
        var lineNumber = lineSpan.StartLinePosition.Line + 1;

        references.Add(new TypeReferenceEntity(
            namedType.Name,
            namedType.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            kind,
            usageKind,
            lineNumber));
    }

    /// <summary>
    ///     Recursively collects type references from pattern syntax nodes.
    /// </summary>
    public static void CollectPatternTypeReferences(List<TypeReferenceEntity> references, PatternSyntax pattern,
        SyntaxTree tree, SemanticModel semanticModel)
    {
        switch (pattern)
        {
            case DeclarationPatternSyntax declPattern:
                AddTypeReference(references, declPattern.Type, "PatternMatch", tree, semanticModel);
                break;

            case RecursivePatternSyntax recursivePattern:
                if (recursivePattern.Type != null)
                    AddTypeReference(references, recursivePattern.Type, "PatternMatch", tree, semanticModel);
                break;

            case BinaryPatternSyntax binaryPattern:
                CollectPatternTypeReferences(references, binaryPattern.Left, tree, semanticModel);
                CollectPatternTypeReferences(references, binaryPattern.Right, tree, semanticModel);
                break;

            case UnaryPatternSyntax unaryPattern:
                CollectPatternTypeReferences(references, unaryPattern.Pattern, tree, semanticModel);
                break;
        }
    }

    /// <summary>
    ///     Gets a string representation of the type kind.
    /// </summary>
    public static string GetTypeKindString(INamedTypeSymbol namedType)
    {
        return namedType.TypeKind switch
        {
            TypeKind.Interface => "Interface",
            TypeKind.Class => "Class",
            TypeKind.Enum => "Enum",
            TypeKind.Struct => "Struct",
            TypeKind.Delegate => "Delegate",
            _ => "Other"
        };
    }
}

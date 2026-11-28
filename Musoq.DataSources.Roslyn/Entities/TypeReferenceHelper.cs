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
    public static IEnumerable<TypeReferenceEntity> ReadTypeReferences(SyntaxNode body, SemanticModel semanticModel)
    {
        var tree = body.SyntaxTree;
        foreach (var node in body.DescendantNodes())
        {
            switch (node)
            {
                case LocalDeclarationStatementSyntax { Declaration.Type.IsVar: true } local:
                    foreach (var variable in local.Declaration.Variables)
                        if (variable.Initializer?.Value is { } value &&
                            semanticModel.GetTypeInfo(value).Type is INamedTypeSymbol { SpecialType: SpecialType.None } type)
                            yield return CreateReference(type, local, "LocalVariable", tree);
                    continue;

                case GenericNameSyntax generic:
                    foreach (var argument in generic.TypeArgumentList.Arguments)
                        if (ResolveReference(argument, "GenericArgument", tree, semanticModel) is { } reference)
                            yield return reference;
                    continue;
            }

            var pattern = node switch
            {
                IsPatternExpressionSyntax expression => expression.Pattern,
                SwitchExpressionArmSyntax arm => arm.Pattern,
                CasePatternSwitchLabelSyntax label => label.Pattern,
                _ => null
            };
            if (pattern is not null)
            {
                foreach (var type in ReadPatternTypes(pattern))
                    if (ResolveReference(type, "PatternMatch", tree, semanticModel) is { } reference)
                        yield return reference;
                continue;
            }

            (TypeSyntax? typeSyntax, string usage) = node switch
            {
                CastExpressionSyntax cast => (cast.Type, "Cast"),
                BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression, Right: TypeSyntax type } => (type, "Is"),
                BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression, Right: TypeSyntax type } => (type, "As"),
                LocalDeclarationStatementSyntax local => (local.Declaration.Type, "LocalVariable"),
                ObjectCreationExpressionSyntax creation => (creation.Type, "ObjectCreation"),
                TypeOfExpressionSyntax typeOf => (typeOf.Type, "TypeOf"),
                DefaultExpressionSyntax expression => (expression.Type, "Default"),
                ArrayCreationExpressionSyntax array => (array.Type.ElementType, "ArrayCreation"),
                CatchDeclarationSyntax declaration => (declaration.Type, "CatchDeclaration"),
                _ => (null, string.Empty)
            };
            if (typeSyntax is not null && ResolveReference(typeSyntax, usage, tree, semanticModel) is { } found)
                yield return found;
        }
    }

    private static TypeReferenceEntity? ResolveReference(
        TypeSyntax syntax, string usage, SyntaxTree tree, SemanticModel semanticModel)
    {
        var type = semanticModel.GetTypeInfo(syntax).Type ?? semanticModel.GetSymbolInfo(syntax).Symbol as ITypeSymbol;
        return type is INamedTypeSymbol { SpecialType: SpecialType.None } named
            ? CreateReference(named, syntax, usage, tree)
            : null;
    }

    private static TypeReferenceEntity CreateReference(
        INamedTypeSymbol type, SyntaxNode syntax, string usage, SyntaxTree tree)
    {
        var line = tree.GetLineSpan(syntax.Span).StartLinePosition.Line + 1;
        return new TypeReferenceEntity(type.Name, type.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            GetTypeKindString(type), usage, line);
    }

    private static IEnumerable<TypeSyntax> ReadPatternTypes(PatternSyntax pattern)
    {
        switch (pattern)
        {
            case DeclarationPatternSyntax declaration:
                yield return declaration.Type;
                break;
            case RecursivePatternSyntax { Type: { } type }:
                yield return type;
                break;
            case BinaryPatternSyntax binary:
                foreach (var child in ReadPatternTypes(binary.Left))
                    yield return child;
                foreach (var child in ReadPatternTypes(binary.Right))
                    yield return child;
                break;
            case UnaryPatternSyntax unary:
                foreach (var child in ReadPatternTypes(unary.Pattern))
                    yield return child;
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

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
/// Represents a field entity that provides information about a field in the source code.
/// </summary>
public class FieldEntity
{
    private readonly IFieldSymbol _fieldSymbol;
    private readonly VariableDeclaratorSyntax? _syntax;
    private readonly Solution? _solution;

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldEntity"/> class.
    /// </summary>
    /// <param name="fieldSymbol">The field symbol representing the field.</param>
    /// <param name="syntax">The variable declarator syntax node.</param>
    public FieldEntity(IFieldSymbol fieldSymbol, VariableDeclaratorSyntax? syntax = null)
    {
        _fieldSymbol = fieldSymbol;
        _syntax = syntax;
        _solution = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldEntity"/> class with solution context.
    /// </summary>
    /// <param name="fieldSymbol">The field symbol representing the field.</param>
    /// <param name="syntax">The variable declarator syntax node.</param>
    /// <param name="solution">The solution for finding references.</param>
    public FieldEntity(IFieldSymbol fieldSymbol, VariableDeclaratorSyntax? syntax, Solution solution)
    {
        _fieldSymbol = fieldSymbol;
        _syntax = syntax;
        _solution = solution;
    }

    /// <summary>
    /// Gets the name of the field.
    /// </summary>
    public string Name => _fieldSymbol.Name;

    /// <summary>
    /// Gets the type of the field as a string.
    /// </summary>
    public string Type => _fieldSymbol.Type.Name;

    /// <summary>
    /// Gets the full type name including namespace.
    /// </summary>
    public string FullTypeName => _fieldSymbol.Type.ToDisplayString();

    /// <summary>
    /// Gets a value indicating whether the field is read-only.
    /// </summary>
    public bool IsReadOnly => _fieldSymbol.IsReadOnly;

    /// <summary>
    /// Gets a value indicating whether the field is constant.
    /// </summary>
    public bool IsConst => _fieldSymbol.IsConst;

    /// <summary>
    /// Gets a value indicating whether the field is static.
    /// </summary>
    public bool IsStatic => _fieldSymbol.IsStatic;

    /// <summary>
    /// Gets a value indicating whether the field is volatile.
    /// </summary>
    public bool IsVolatile => _fieldSymbol.IsVolatile;

    /// <summary>
    /// Gets a value indicating whether the field has a fixed size buffer.
    /// </summary>
    public bool IsFixedSizeBuffer => _fieldSymbol.IsFixedSizeBuffer;

    /// <summary>
    /// Gets the constant value if the field is a constant, null otherwise.
    /// </summary>
    public object? ConstantValue => _fieldSymbol.HasConstantValue ? _fieldSymbol.ConstantValue : null;

    /// <summary>
    /// Gets a value indicating whether the field has an initializer.
    /// </summary>
    public bool HasInitializer => _syntax?.Initializer != null;

    /// <summary>
    /// Gets the initializer expression text if present.
    /// </summary>
    public string? InitializerText => _syntax?.Initializer?.Value.ToString();

    /// <summary>
    /// Gets the accessibility of the field (public, private, etc.).
    /// </summary>
    public string Accessibility => _fieldSymbol.DeclaredAccessibility.ToString().ToLowerInvariant();

    /// <summary>
    /// Gets a value indicating whether the field is an implicitly declared backing field.
    /// </summary>
    public bool IsImplicitlyDeclared => _fieldSymbol.IsImplicitlyDeclared;

    /// <summary>
    /// Gets the number of references to this field in the solution.
    /// Returns null if the solution context is not available.
    /// </summary>
    public int? ReferenceCount
    {
        get
        {
            if (_solution == null)
                return null;

            var references = RoslynAsyncHelper.RunSync(SymbolFinder.FindReferencesAsync(_fieldSymbol, _solution));
            return references.Sum(r => r.Locations.Count());
        }
    }

    /// <summary>
    /// Gets a value indicating whether the field is used (referenced) in the solution.
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
    /// Gets the modifiers of the field (e.g., public, private, protected, static, readonly).
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<string> Modifiers
    {
        get
        {
            var syntaxRef = _fieldSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            var fieldDeclaration = syntaxRef?.GetSyntax()?.Parent?.Parent as FieldDeclarationSyntax;
            
            if (fieldDeclaration == null)
                return [];

            return fieldDeclaration.Modifiers
                .Where(token => 
                    token.IsKind(SyntaxKind.PublicKeyword) ||
                    token.IsKind(SyntaxKind.PrivateKeyword) ||
                    token.IsKind(SyntaxKind.ProtectedKeyword) ||
                    token.IsKind(SyntaxKind.InternalKeyword) ||
                    token.IsKind(SyntaxKind.StaticKeyword) ||
                    token.IsKind(SyntaxKind.ReadOnlyKeyword) ||
                    token.IsKind(SyntaxKind.ConstKeyword) ||
                    token.IsKind(SyntaxKind.VolatileKeyword) ||
                    token.IsKind(SyntaxKind.NewKeyword))
                .Select(token => token.ValueText);
        }
    }

    /// <summary>
    /// Gets the attributes applied to the field.
    /// </summary>
    [BindablePropertyAsTable]
    public IEnumerable<AttributeEntity> Attributes => 
        _fieldSymbol.GetAttributes().Select(attr => new AttributeEntity(attr));

    /// <summary>
    /// Returns a string representation of the field entity.
    /// </summary>
    /// <returns>A string representing the field.</returns>
    public override string ToString()
    {
        var modifiers = string.Join(" ", Modifiers);
        return $"{modifiers} {Type} {Name}".Trim();
    }
}

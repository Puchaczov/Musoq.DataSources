namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
///     Represents a type reference found within a code body (method, constructor, or property accessor),
///     with information about how the type is used (e.g., cast, is pattern, as expression, local variable type, etc.).
/// </summary>
public class TypeReferenceEntity
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TypeReferenceEntity" /> class.
    /// </summary>
    /// <param name="name">The name of the referenced type.</param>
    /// <param name="namespaceName">The namespace of the referenced type.</param>
    /// <param name="kind">The kind of type (Class, Interface, Enum, Struct, etc.).</param>
    /// <param name="usageKind">How the type is used (Cast, Is, As, PatternMatch, LocalVariable, GenericArgument, etc.).</param>
    /// <param name="lineNumber">The line number where the reference occurs.</param>
    public TypeReferenceEntity(string name, string namespaceName, string kind, string usageKind, int lineNumber)
    {
        Name = name;
        Namespace = namespaceName;
        Kind = kind;
        UsageKind = usageKind;
        LineNumber = lineNumber;
    }

    /// <summary>
    ///     Gets the name of the referenced type.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the namespace of the referenced type.
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    ///     Gets the kind of type (Class, Interface, Enum, Struct, Delegate, TypeParameter, etc.).
    /// </summary>
    public string Kind { get; }

    /// <summary>
    ///     Gets the fully qualified name of the referenced type.
    /// </summary>
    public string FullName => string.IsNullOrEmpty(Namespace) ? Name : $"{Namespace}.{Name}";

    /// <summary>
    ///     Gets how the type is used at this reference site.
    ///     Possible values: Cast, Is, As, PatternMatch, LocalVariable, ObjectCreation,
    ///     GenericArgument, TypeOf, Default, ReturnType, BaseList, Constraint, ArrayCreation,
    ///     CatchDeclaration, Other.
    /// </summary>
    public string UsageKind { get; }

    /// <summary>
    ///     Gets the line number (1-based) where the type reference occurs.
    /// </summary>
    public int LineNumber { get; }

    /// <summary>
    ///     Gets a value indicating whether the referenced type is an interface.
    /// </summary>
    public bool IsInterface => Kind == "Interface";

    /// <summary>
    ///     Gets a value indicating whether the referenced type is a class.
    /// </summary>
    public bool IsClass => Kind == "Class";

    /// <summary>
    ///     Gets a value indicating whether the referenced type is an enum.
    /// </summary>
    public bool IsEnum => Kind == "Enum";

    /// <summary>
    ///     Gets a value indicating whether the referenced type is a struct.
    /// </summary>
    public bool IsStruct => Kind == "Struct";

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{FullName} ({UsageKind})";
    }
}

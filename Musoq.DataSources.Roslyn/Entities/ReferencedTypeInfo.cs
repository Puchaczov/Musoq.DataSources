namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
///     Represents information about a type referenced in a document.
/// </summary>
public class ReferencedTypeInfo
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ReferencedTypeInfo" /> class.
    /// </summary>
    /// <param name="name">The name of the type.</param>
    /// <param name="namespaceName">The namespace of the type.</param>
    /// <param name="kind">The kind of type (class, interface, enum, struct, etc.).</param>
    public ReferencedTypeInfo(string name, string namespaceName, string kind)
    {
        Name = name;
        Namespace = namespaceName;
        Kind = kind;
    }

    /// <summary>
    ///     Gets the name of the type.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the namespace of the type.
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    ///     Gets the kind of type (Class, Interface, Enum, Struct, etc.).
    /// </summary>
    public string Kind { get; }

    /// <summary>
    ///     Gets the fully qualified name of the type.
    /// </summary>
    public string FullName => string.IsNullOrEmpty(Namespace) ? Name : $"{Namespace}.{Name}";

    /// <inheritdoc />
    public override string ToString()
    {
        return FullName;
    }
}
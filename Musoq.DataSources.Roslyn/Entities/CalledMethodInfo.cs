namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents information about a method that is called by another method.
/// </summary>
public class CalledMethodInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalledMethodInfo"/> class.
    /// </summary>
    /// <param name="name">The name of the called method.</param>
    /// <param name="containingTypeName">The name of the type containing the called method.</param>
    /// <param name="containingNamespace">The namespace containing the called method.</param>
    /// <param name="isStatic">Whether the called method is static.</param>
    /// <param name="isExtensionMethod">Whether the called method is an extension method.</param>
    public CalledMethodInfo(string name, string containingTypeName, string containingNamespace, bool isStatic, bool isExtensionMethod)
    {
        Name = name;
        ContainingTypeName = containingTypeName;
        ContainingNamespace = containingNamespace;
        IsStatic = isStatic;
        IsExtensionMethod = isExtensionMethod;
    }

    /// <summary>
    /// Gets the name of the called method.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the name of the type containing the called method.
    /// </summary>
    public string ContainingTypeName { get; }

    /// <summary>
    /// Gets the namespace containing the called method.
    /// </summary>
    public string ContainingNamespace { get; }

    /// <summary>
    /// Gets a value indicating whether the called method is static.
    /// </summary>
    public bool IsStatic { get; }

    /// <summary>
    /// Gets a value indicating whether the called method is an extension method.
    /// </summary>
    public bool IsExtensionMethod { get; }

    /// <summary>
    /// Gets the fully qualified method name.
    /// </summary>
    public string FullName => string.IsNullOrEmpty(ContainingNamespace) 
        ? $"{ContainingTypeName}.{Name}" 
        : $"{ContainingNamespace}.{ContainingTypeName}.{Name}";

    /// <inheritdoc />
    public override string ToString() => FullName;
}

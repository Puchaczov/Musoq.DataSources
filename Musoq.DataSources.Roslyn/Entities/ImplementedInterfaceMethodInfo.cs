namespace Musoq.DataSources.Roslyn.Entities;

/// <summary>
/// Represents information about an interface method that is implemented by a class method.
/// </summary>
public class ImplementedInterfaceMethodInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImplementedInterfaceMethodInfo"/> class.
    /// </summary>
    /// <param name="methodName">The name of the interface method.</param>
    /// <param name="interfaceName">The name of the interface.</param>
    /// <param name="interfaceNamespace">The namespace of the interface.</param>
    public ImplementedInterfaceMethodInfo(string methodName, string interfaceName, string interfaceNamespace)
    {
        MethodName = methodName;
        InterfaceName = interfaceName;
        InterfaceNamespace = interfaceNamespace;
    }

    /// <summary>
    /// Gets the name of the interface method.
    /// </summary>
    public string MethodName { get; }

    /// <summary>
    /// Gets the name of the interface.
    /// </summary>
    public string InterfaceName { get; }

    /// <summary>
    /// Gets the namespace of the interface.
    /// </summary>
    public string InterfaceNamespace { get; }

    /// <summary>
    /// Gets the fully qualified interface name.
    /// </summary>
    public string FullInterfaceName => string.IsNullOrEmpty(InterfaceNamespace) 
        ? InterfaceName 
        : $"{InterfaceNamespace}.{InterfaceName}";

    /// <inheritdoc />
    public override string ToString() => $"{FullInterfaceName}.{MethodName}";
}

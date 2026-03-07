namespace Solution1.ClassLibrary1;

/// <summary>
/// Interface for testing type reference detection in method bodies.
/// </summary>
public interface IProcessable
{
    void Process();
    
    string Name { get; }
}

/// <summary>
/// Another interface that extends IProcessable, for testing AllInterfaces (transitive).
/// </summary>
public interface IAdvancedProcessable : IProcessable
{
    void ProcessAdvanced();
}

/// <summary>
/// Third-level interface for deeper transitive testing.
/// </summary>
public interface ISuperAdvancedProcessable : IAdvancedProcessable
{
    void ProcessSuperAdvanced();
}

/// <summary>
/// A concrete class implementing the deep interface hierarchy.
/// Only directly implements ISuperAdvancedProcessable, but transitively implements
/// IAdvancedProcessable and IProcessable.
/// </summary>
public class DeepImplementor : ISuperAdvancedProcessable
{
    public void Process() { }
    public string Name => "Deep";
    public void ProcessAdvanced() { }
    public void ProcessSuperAdvanced() { }
}

/// <summary>
/// Class with methods that use interfaces in various ways within method bodies.
/// Used to test MethodEntity.ReferencedTypes with different UsageKind values.
/// </summary>
public class InterfaceUsagePatterns
{
    /// <summary>
    /// Uses cast expression: (IProcessable)obj
    /// </summary>
    public void MethodWithCast(object obj)
    {
        var processed = (IProcessable)obj;
        processed.Process();
    }
    
    /// <summary>
    /// Uses 'is' operator: obj is IProcessable
    /// </summary>
    public bool MethodWithIsOperator(object obj)
    {
        return obj is IProcessable;
    }
    
    /// <summary>
    /// Uses 'as' operator: obj as IProcessable
    /// </summary>
    public void MethodWithAsOperator(object obj)
    {
        var processed = obj as IProcessable;
        processed?.Process();
    }
    
    /// <summary>
    /// Uses pattern matching: obj is IProcessable p
    /// </summary>
    public void MethodWithPatternMatch(object obj)
    {
        if (obj is IProcessable p)
        {
            p.Process();
        }
    }
    
    /// <summary>
    /// Uses interface as local variable type
    /// </summary>
    public void MethodWithLocalVariable()
    {
        IProcessable item = new DeepImplementor();
        item.Process();
    }
    
    /// <summary>
    /// Uses interface as generic type argument: List&lt;IProcessable&gt;
    /// </summary>
    public void MethodWithGenericArgument()
    {
        var list = new List<IProcessable>();
        list.Add(new DeepImplementor());
    }
    
    /// <summary>
    /// Uses typeof(IProcessable)
    /// </summary>
    public Type MethodWithTypeOf()
    {
        return typeof(IProcessable);
    }
    
    /// <summary>
    /// Uses default(IProcessable)
    /// </summary>
    public IProcessable? MethodWithDefault()
    {
        return default(IProcessable);
    }
    
    /// <summary>
    /// Uses interface in catch declaration
    /// </summary>
    public void MethodWithMultipleUsages(object obj)
    {
        if (obj is IProcessable processable)
        {
            processable.Process();
        }

        var casted = (IAdvancedProcessable)obj;
        casted.ProcessAdvanced();
        
        var asResult = obj as ISuperAdvancedProcessable;
        asResult?.ProcessSuperAdvanced();
    }
    
    /// <summary>
    /// Uses interface in array creation
    /// </summary>
    public void MethodWithArrayCreation()
    {
        var items = new IProcessable[10];
        items[0] = new DeepImplementor();
    }
    
    /// <summary>
    /// Uses interface in switch pattern matching
    /// </summary>
    public string MethodWithSwitchPattern(object obj)
    {
        switch (obj)
        {
            case IProcessable p:
                return p.Name;
            case IAdvancedProcessable ap:
                return "advanced";
            default:
                return "unknown";
        }
    }
    
    /// <summary>
    /// Uses var-inferred local variable whose type is an interface
    /// </summary>
    public void MethodWithVarInferred()
    {
        IProcessable source = new DeepImplementor();
        var inferred = source as IAdvancedProcessable;
        var cast = (IProcessable)new DeepImplementor();
    }
}

/// <summary>
/// Class with a constructor that uses interfaces in the constructor body.
/// </summary>
public class ConstructorUsagePatterns
{
    private readonly IProcessable _processable;
    
    /// <summary>
    /// Constructor that casts and uses pattern matching
    /// </summary>
    public ConstructorUsagePatterns(object obj)
    {
        _processable = (IProcessable)obj;
        
        if (obj is IAdvancedProcessable advanced)
        {
            advanced.ProcessAdvanced();
        }
        
        IProcessable local = new DeepImplementor();
        local.Process();
    }
}

/// <summary>
/// Class with properties that have accessor bodies using interfaces.
/// </summary>
public class PropertyUsagePatterns
{
    private object _value = new object();
    
    /// <summary>
    /// Property with getter that casts to an interface
    /// </summary>
    public IProcessable? CastingProperty
    {
        get
        {
            return _value as IProcessable;
        }
        set
        {
            if (value is IAdvancedProcessable advanced)
            {
                advanced.ProcessAdvanced();
            }
            IProcessable backup = new DeepImplementor();
            _value = value ?? backup;
        }
    }
    
    /// <summary>
    /// Expression-bodied property that uses typeof
    /// </summary>
    public Type ProcessableType => typeof(IProcessable);
}

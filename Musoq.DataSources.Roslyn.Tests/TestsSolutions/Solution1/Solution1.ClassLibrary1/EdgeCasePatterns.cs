namespace Solution1.ClassLibrary1;

/// <summary>
/// Class with no interface implementations at all - tests empty AllInterfaces
/// </summary>
public class NoInterfaceClass
{
    public void DoWork() { }
}

/// <summary>
/// Class implementing a single interface (no transitivity) - tests AllInterfaces with one entry
/// </summary>
public class SingleInterfaceClass : IProcessable
{
    public void Process() { }
    public string Name => "Single";
}

/// <summary>
/// Interface with no parents - tests empty AllBaseInterfaces
/// </summary>
public interface IStandaloneInterface
{
    void Standalone();
}

/// <summary>
/// Interface with exactly one parent - tests single-level AllBaseInterfaces
/// </summary>
public interface IChildInterface : IStandaloneInterface
{
    void ChildMethod();
}

/// <summary>
/// Class with method that has an empty body (no type references)
/// </summary>
public class EmptyBodyClass
{
    public void MethodWithNoReferences()
    {
        // no type references at all
        var x = 42;
        var y = "hello";
    }
    
    public void MethodWithOnlyClassReferences()
    {
        var obj = new DeepImplementor();
        var list = new List<string>();
    }
    
    /// <summary>
    /// Method using try-catch with exception type
    /// </summary>
    public void MethodWithCatchDeclaration()
    {
        try
        {
            var x = 1;
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine(ex.Message);
        }
        catch (ArgumentNullException ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
    
    /// <summary>
    /// Method with ObjectCreation of interface-typed variable
    /// </summary>
    public void MethodWithObjectCreation()
    {
        var impl = new DeepImplementor();
        IProcessable p = new DeepImplementor();
    }
}

/// <summary>
/// Class with constructor edge cases
/// </summary>
public class ConstructorEdgeCases
{
    private readonly List<IProcessable> _items;
    
    /// <summary>
    /// Default constructor with no body logic
    /// </summary>
    public ConstructorEdgeCases()
    {
        _items = new List<IProcessable>();
    }
    
    /// <summary>
    /// Constructor with multiple local variables and varied type references
    /// </summary>
    public ConstructorEdgeCases(IProcessable first, IAdvancedProcessable second)
    {
        _items = new List<IProcessable>();
        IProcessable localFirst = first;
        IAdvancedProcessable localSecond = second;
        var combined = new List<IProcessable> { localFirst, localSecond };
        
        try
        {
            localFirst.Process();
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine(ex.Message);
        }
        
        if (first is ISuperAdvancedProcessable super)
        {
            super.ProcessSuperAdvanced();
        }
        
        var array = new IProcessable[5];
        var type = typeof(IAdvancedProcessable);
    }
}

/// <summary>
/// Class with property edge cases
/// </summary>
public class PropertyEdgeCases
{
    /// <summary>
    /// Auto-property with no accessor body - should have no referenced types
    /// </summary>
    public IProcessable? AutoProperty { get; set; }
    
    /// <summary>
    /// Read-only auto-property - should have no referenced types
    /// </summary>
    public string ReadOnlyAutoProperty { get; } = "default";
    
    /// <summary>
    /// Property with getter-only body containing type references
    /// </summary>
    public IProcessable? GetterOnlyWithBody
    {
        get
        {
            var impl = new DeepImplementor();
            return impl as IProcessable;
        }
    }
    
    /// <summary>
    /// Property with multiple local variables in setter
    /// </summary>
    private IProcessable? _stored;
    public IProcessable? MultiLocalProperty
    {
        get { return _stored; }
        set
        {
            IProcessable? backup = _stored;
            IAdvancedProcessable? advanced = value as IAdvancedProcessable;
            string description = "updating";
            _stored = value;
        }
    }
    
    /// <summary>
    /// Generic type property for testing FullTypeName with generics
    /// </summary>
    public List<IProcessable> GenericProperty { get; set; } = new();
    
    /// <summary>
    /// Nullable value type property for testing FullTypeName with nullable
    /// </summary>
    public int? NullableValueProperty { get; set; }
    
    /// <summary>
    /// Dictionary property for complex generic FullTypeName
    /// </summary>
    public Dictionary<string, IProcessable> DictionaryProperty { get; set; } = new();
}

/// <summary>
/// Class with parameter edge cases
/// </summary>
public class ParameterEdgeCases
{
    /// <summary>
    /// Method with generic parameter type
    /// </summary>
    public void MethodWithGenericParam(List<IProcessable> items) { }
    
    /// <summary>
    /// Method with array parameter type
    /// </summary>
    public void MethodWithArrayParam(IProcessable[] items) { }
    
    /// <summary>
    /// Method with nullable value type parameter
    /// </summary>
    public void MethodWithNullableParam(int? value) { }
    
    /// <summary>
    /// Method with interface parameter
    /// </summary>
    public void MethodWithInterfaceParam(IProcessable processable) { }
    
    /// <summary>
    /// Method with multiple varied parameters
    /// </summary>
    public void MethodWithMixedParams(
        string name,
        IProcessable processable,
        List<int> numbers,
        Dictionary<string, IAdvancedProcessable> lookup)
    { }
}

/// <summary>
/// Class with method return type edge cases
/// </summary>
public class ReturnTypeEdgeCases
{
    public void VoidMethod() { }
    
    public List<IProcessable> GenericReturnMethod() => new();
    
    public Task<IProcessable> TaskReturnMethod() => Task.FromResult<IProcessable>(new DeepImplementor());
    
    public IProcessable[] ArrayReturnMethod() => Array.Empty<IProcessable>();
    
    public int? NullableReturnEdgeCaseMethod() => null;
    
    public Dictionary<string, List<IProcessable>> NestedGenericReturnMethod() => new();
}

/// <summary>
/// Struct with interfaces and constructor patterns for struct-specific tests
/// </summary>
public struct StructWithPatterns : IProcessable
{
    private readonly object _data;
    
    public StructWithPatterns(object data)
    {
        _data = data;
        if (data is IProcessable processable)
        {
            processable.Process();
        }
        IProcessable local = new DeepImplementor();
        local.Process();
    }
    
    public void Process() { }
    
    public string Name => "StructPattern";
    
    /// <summary>
    /// Struct method with referenced types
    /// </summary>
    public void ProcessData()
    {
        var casted = (IProcessable)_data;
        casted.Process();
        
        if (_data is IAdvancedProcessable advanced)
        {
            advanced.ProcessAdvanced();
        }
    }
    
    /// <summary>
    /// Struct property with accessor body
    /// </summary>
    public IProcessable? ProcessableData
    {
        get
        {
            return _data as IProcessable;
        }
    }
}

/// <summary>
/// Class for testing var inference in constructors
/// </summary>
public class VarInferenceInConstructor
{
    public VarInferenceInConstructor(object obj)
    {
        var casted = (IProcessable)obj;
        var asResult = obj as IAdvancedProcessable;
        IProcessable explicit1 = new DeepImplementor();
        var fromExplicit = explicit1;
    }
}

/// <summary>
/// Class for comprehensive cross-entity query: finding all interface usage across
/// methods, constructors, and properties
/// </summary>
public class ComprehensiveInterfaceUser
{
    private readonly IProcessable _field;
    
    /// <summary>
    /// Constructor uses IProcessable via cast
    /// </summary>
    public ComprehensiveInterfaceUser(object obj)
    {
        _field = (IProcessable)obj;
    }
    
    /// <summary>
    /// Method uses IAdvancedProcessable via pattern match
    /// </summary>
    public void UseAdvanced(object obj)
    {
        if (obj is IAdvancedProcessable advanced)
        {
            advanced.ProcessAdvanced();
        }
    }
    
    /// <summary>
    /// Property uses ISuperAdvancedProcessable via as
    /// </summary>
    public ISuperAdvancedProcessable? SuperFeature
    {
        get
        {
            return _field as ISuperAdvancedProcessable;
        }
    }
}

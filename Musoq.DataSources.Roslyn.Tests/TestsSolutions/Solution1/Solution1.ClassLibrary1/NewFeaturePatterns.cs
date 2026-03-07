using System;
using System.Collections.Generic;

namespace Solution1.ClassLibrary1;

// ============================================================
// Phase 1 Test Code: IsRecord, IsPartial, Property locations
// ============================================================

/// <summary>
/// Record class for testing IsRecord property
/// </summary>
public record RecordClass(string Name, int Age);

/// <summary>
/// Record class with body for testing IsRecord property
/// </summary>
public record RecordClassWithBody(string Name)
{
    public string UpperName => Name.ToUpper();
}

/// <summary>
/// Partial class part 1 for testing IsPartial property
/// </summary>
public partial class PartialFeatureClass
{
    public void MethodInPart1() { }
}

/// <summary>
/// Partial class part 2 for testing IsPartial property
/// </summary>
public partial class PartialFeatureClass
{
    public void MethodInPart2() { }
}

/// <summary>
/// Partial struct for testing IsPartial on struct
/// </summary>
public partial struct PartialFeatureStruct
{
    public int Value { get; set; }
}

/// <summary>
/// Partial interface for testing IsPartial on interface
/// </summary>
public partial interface IPartialFeatureInterface
{
    void DoSomething();
}

// ============================================================
// Phase 2 Test Code: Attributes on entities
// ============================================================

/// <summary>
/// Custom attribute for testing
/// </summary>
[AttributeUsage(AttributeTargets.All)]
public class TestCustomAttribute : Attribute
{
    public string Description { get; set; }
    
    public TestCustomAttribute() { }
    public TestCustomAttribute(string description) { Description = description; }
}

/// <summary>
/// Class with attributes on properties, for testing PropertyEntity.Attributes
/// </summary>
public class AttributeTestClass
{
    [Obsolete("Use NewProperty instead")]
    public string OldProperty { get; set; }
    
    [TestCustom("This is new")]
    public string NewProperty { get; set; }
    
    public string NoAttributeProperty { get; set; }
    
    /// <summary>
    /// Method with attributed parameters 
    /// </summary>
    public void MethodWithAttributedParams(
        [TestCustom("param1")] string name,
        [TestCustom] int value)
    { }
}

/// <summary>
/// Attributed interface
/// </summary>
[TestCustom("Interface description")]
public interface IAttributedInterface
{
    void Do();
}

/// <summary>
/// Attributed enum
/// </summary>
[Flags]
public enum FlagsEnum
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
    All = Read | Write | Execute
}

/// <summary>
/// Non-flags enum with underlying byte type
/// </summary>
public enum ByteEnum : byte
{
    Low = 0,
    Medium = 128,
    High = 255
}

// ============================================================
// Phase 3 Test Code: Parameter enhancements
// ============================================================

/// <summary>
/// Class for testing parameter defaults, ordinals
/// </summary>
public class ParameterTestClass
{
    public void MethodWithDefaults(
        string required,
        int optional = 42,
        string optionalStr = "hello",
        bool flag = true)
    { }
    
    public void MethodWithNoDefaults(string a, int b, bool c)
    { }
}

// ============================================================
// Phase 5-6 Test Code: Enum/Interface enhancements
// ============================================================

/// <summary>
/// Interface with events
/// </summary>
public interface IEventInterface
{
    event EventHandler ItemAdded;
    event EventHandler<string> ItemRemoved;
}

/// <summary>
/// Interface with type parameter
/// </summary>
public interface IGenericInterface<T>
{
    T GetValue();
    void SetValue(T value);
}

/// <summary>
/// Interface extending generic
/// </summary>
public interface ISpecificInterface : IGenericInterface<string>
{
    void DoSpecific();
}

// ============================================================
// Phase 7 Test Code: Type constraints
// ============================================================

/// <summary>
/// Class with constrained generic type parameters
/// </summary>
public class ConstrainedGenericClass<T, TKey, TValue>
    where T : class, IDisposable, new()
    where TKey : struct
    where TValue : notnull
{
    public T CreateItem() => new T();
    
    /// <summary>
    /// Method with its own type constraints
    /// </summary>
    public TResult Transform<TResult>(T input) where TResult : class, new()
    {
        return new TResult();
    }
    
    public void NoConstraintMethod<TUnconstrained>(TUnconstrained value)
    { }
}

// ============================================================
// Phase 8 Test Code: Explicit interface implementations
// ============================================================

/// <summary>
/// Interface for explicit implementation testing
/// </summary>
public interface IExplicitTestA
{
    void SharedMethod();
    string SharedProperty { get; }
}

/// <summary>
/// Second interface for explicit implementation testing
/// </summary>
public interface IExplicitTestB
{
    void SharedMethod();
    string SharedProperty { get; }
}

/// <summary>
/// Class with explicit interface implementations
/// </summary>
public class ExplicitImplementor : IExplicitTestA, IExplicitTestB
{
    // Explicit implementation of IExplicitTestA
    void IExplicitTestA.SharedMethod() { }
    string IExplicitTestA.SharedProperty => "A";
    
    // Explicit implementation of IExplicitTestB
    void IExplicitTestB.SharedMethod() { }
    string IExplicitTestB.SharedProperty => "B";
    
    // Regular method
    public void RegularMethod() { }
}

// ============================================================
// Phase 9 Test Code: Delegates
// ============================================================

/// <summary>
/// Simple delegate
/// </summary>
public delegate void SimpleCallback(string message);

/// <summary>
/// Generic delegate with return type
/// </summary>
public delegate TResult Transformer<in TInput, out TResult>(TInput input);

/// <summary>
/// Delegate with no parameters
/// </summary>
public delegate int ValueProvider();

// ============================================================
// Phase 10 Test Code: Method callers
// ============================================================

/// <summary>
/// Class for caller tracking tests
/// </summary>
public class CallerTestClass
{
    /// <summary>
    /// The target method that gets called by multiple callers
    /// </summary>
    public void TargetMethod()
    {
        Console.WriteLine("Target");
    }
    
    /// <summary>
    /// First caller of TargetMethod
    /// </summary>
    public void Caller1()
    {
        TargetMethod();
    }
    
    /// <summary>
    /// Second caller of TargetMethod
    /// </summary>
    public void Caller2()
    {
        TargetMethod();
        TargetMethod(); // calls twice
    }
    
    /// <summary>
    /// Method that calls nothing additional
    /// </summary>
    public void IsolatedMethod()
    {
        var x = 42;
    }
}

// ============================================================
// Phase 12-13 Test Code: Data flow & Control flow
// ============================================================

/// <summary>
/// Class for data flow / control flow analysis testing
/// </summary>
public class DataFlowTestClass
{
    /// <summary>
    /// Method with lambda that captures variable
    /// </summary>
    public void MethodWithCapture()
    {
        var captured = 42;
        var notCaptured = 10;
        
        Action lambda = () =>
        {
            Console.WriteLine(captured);
        };
        
        lambda();
        Console.WriteLine(notCaptured);
    }
    
    /// <summary>
    /// Method with unreachable code
    /// </summary>
    public int MethodWithUnreachableCode(bool flag)
    {
        if (flag)
            return 1;
        else
            return 2;
        
        // This code is unreachable
        // var x = 42;
    }
    
    /// <summary>
    /// Method that always returns early
    /// </summary>
    public void MethodWithEarlyReturn(int x)
    {
        if (x < 0)
            return;
        
        if (x > 100)
            return;
        
        Console.WriteLine(x);
    }
    
    /// <summary>
    /// Method for testing read/write variable analysis
    /// </summary>
    public int MethodWithReadWrite(int input)
    {
        var result = input * 2;
        var temp = result + 1;
        result = temp - 1;
        return result;
    }
}

// ============================================================
// Property with default value for testing
// ============================================================

/// <summary>
/// Class for property default value tests
/// </summary>
public class PropertyDefaultValueClass
{
    public string PropertyWithDefault { get; set; } = "DefaultValue";
    public int IntPropertyWithDefault { get; set; } = 100;
    public string PropertyWithoutDefault { get; set; }
}

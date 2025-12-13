using System;
using System.Linq;
using System.Reflection;
using Musoq.Schema;

// Get all methods on RuntimeContext that start with "Report"
var type = typeof(RuntimeContext);
var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
    .Where(m => m.Name.StartsWith("Report"))
    .ToList();

Console.WriteLine("RuntimeContext Report methods:");
foreach (var method in methods)
{
    var parameters = method.GetParameters()
        .Select(p => $"{p.ParameterType.Name} {p.Name}")
        .ToList();
    Console.WriteLine($"  {method.Name}({string.Join(", ", parameters)})");
}

// Check constructor signature
var ctors = type.GetConstructors();
Console.WriteLine("\nRuntimeContext constructors:");
foreach (var ctor in ctors)
{
    var parameters = ctor.GetParameters()
        .Select(p => $"{p.ParameterType.Name} {p.Name}")
        .ToList();
    Console.WriteLine($"  RuntimeContext({string.Join(", ", parameters)})");
}

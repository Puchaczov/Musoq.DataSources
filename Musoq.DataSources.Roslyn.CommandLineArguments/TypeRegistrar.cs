using Spectre.Console.Cli;

namespace Musoq.DataSources.Roslyn.CommandLineArguments;

public sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly Dictionary<Type, Func<object>> _registrations = new();

    public void Register(Type service, Type implementation)
    {
        _registrations[service] = () => Activator.CreateInstance(implementation)!;
    }

    public void RegisterInstance(Type service, object implementation)
    {
        _registrations[service] = () => implementation;
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        _registrations[service] = factory;
    }

    public ITypeResolver Build()
    {
        return new TypeResolver(_registrations);
    }

    private sealed class TypeResolver(Dictionary<Type, Func<object>> registrations) : ITypeResolver
    {
        public object? Resolve(Type? type)
        {
            if (type == null)
                return null;

            if (registrations.TryGetValue(type, out var factory))
            {
                return factory();
            }

            // Try to create instance with available constructors
            var constructors = type.GetConstructors();
            if (constructors.Length == 0)
                return null;

            var constructor = constructors[0];
            var parameters = constructor.GetParameters();
            var parameterInstances = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                parameterInstances[i] = Resolve(parameters[i].ParameterType)!;
            }

            return Activator.CreateInstance(type, parameterInstances);
        }
    }
}

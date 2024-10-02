using Microsoft.CodeAnalysis;

namespace Musoq.DataSources.Roslyn.Entities
{
    /// <summary>
    /// Represents a parameter entity with a type and a name.
    /// </summary>
    public class ParameterEntity
    {
        private readonly IParameterSymbol _parameterSymbol;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParameterEntity"/> class.
        /// </summary>
        /// <param name="parameterSymbol">The parameter symbol representing the parameter.</param>
        public ParameterEntity(IParameterSymbol parameterSymbol)
        {
            _parameterSymbol = parameterSymbol;
        }

        /// <summary>
        /// Gets the type of the parameter.
        /// </summary>
        public string Type => _parameterSymbol.Type.Name;

        /// <summary>
        /// Gets the name of the parameter.
        /// </summary>
        public string Name => _parameterSymbol.Name;

        /// <summary>
        /// Gets a value indicating whether the parameter is optional.
        /// </summary>
        public bool IsOptional => _parameterSymbol.IsOptional;

        /// <summary>
        /// Gets a value indicating whether the parameter is a params array.
        /// </summary>
        public bool IsParams => _parameterSymbol.IsParams;

        /// <summary>
        /// Gets a value indicating whether the parameter is the 'this' parameter.
        /// </summary>
        public bool IsThis => _parameterSymbol.IsThis;

        /// <summary>
        /// Gets a value indicating whether the parameter is a discard parameter.
        /// </summary>
        public bool IsDiscard => _parameterSymbol.IsDiscard;

        /// <summary>
        /// Gets a value indicating whether the parameter is passed by 'in' reference.
        /// </summary>
        public bool IsIn => _parameterSymbol.RefKind == RefKind.In;

        /// <summary>
        /// Gets a value indicating whether the parameter is passed by 'out' reference.
        /// </summary>
        public bool IsOut => _parameterSymbol.RefKind == RefKind.Out;

        /// <summary>
        /// Gets a value indicating whether the parameter is passed by 'ref' reference.
        /// </summary>
        public bool IsRef => _parameterSymbol.RefKind == RefKind.Ref;

        /// <summary>
        /// Gets a value indicating whether the parameter is passed by reference (either 'ref' or 'out').
        /// </summary>
        public bool IsByRef => _parameterSymbol.RefKind is RefKind.Ref or RefKind.Out;

        /// <summary>
        /// Gets a value indicating whether the parameter is passed by value.
        /// </summary>
        public bool IsByValue => _parameterSymbol.RefKind == RefKind.None;

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return $"{Type} {Name}";
        }
    }
}
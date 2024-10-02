using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Musoq.DataSources.Roslyn.Entities
{
    /// <summary>
    /// Represents an interface entity in the Roslyn data source.
    /// Inherits from the <see cref="TypeEntity"/> class.
    /// </summary>
    public class InterfaceEntity : TypeEntity
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InterfaceEntity"/> class.
        /// </summary>
        /// <param name="symbol">The Roslyn named type symbol representing the interface.</param>
        public InterfaceEntity(INamedTypeSymbol symbol) : base(symbol) { }

        /// <summary>
        /// Gets the base interfaces implemented by this interface.
        /// </summary>
        public IEnumerable<string> BaseInterfaces => Symbol.Interfaces.Select(i => i.Name);
    }
}
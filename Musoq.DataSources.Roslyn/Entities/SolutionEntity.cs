using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Musoq.DataSources.Roslyn.Components;
using Musoq.Plugins.Attributes;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.Roslyn.Entities
{
    /// <summary>
    /// Represents a solution entity that provides access to projects within a solution.
    /// </summary>
    public class SolutionEntity
    {
        private readonly Solution _solution;
        private ProjectEntity[] _projects;
        private bool _wasLoaded;

        private readonly CancellationToken _cancellationToken;
        private readonly NuGetPackageMetadataRetriever _nuGetPackageMetadataRetriever;
        
        /// <summary>
        /// A read-only dictionary mapping column names to their respective indices.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, int> NameToIndexMap;

        /// <summary>
        /// A read-only dictionary mapping column indices to functions that access the corresponding properties.
        /// </summary>
        public static readonly IReadOnlyDictionary<int, Func<SolutionEntity, object?>> IndexToObjectAccessMap;

        /// <summary>
        /// An array of schema columns representing the structure of the solution entity.
        /// </summary>
        public static readonly ISchemaColumn[] Columns =
        [
            new SchemaColumn(nameof(Id), 0, typeof(string)),
            new SchemaColumn(nameof(Projects), 1, typeof(ProjectEntity[]))
        ];

        /// <summary>
        /// Initializes a new instance of the <see cref="SolutionEntity"/> class.
        /// </summary>
        /// <param name="solution">The solution.</param>
        /// <param name="nuGetPackageMetadataRetriever">The NuGet package metadata retriever.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public SolutionEntity(Solution solution, NuGetPackageMetadataRetriever nuGetPackageMetadataRetriever, CancellationToken cancellationToken)
        {
            _solution = solution;
            _nuGetPackageMetadataRetriever = nuGetPackageMetadataRetriever;
            _projects = [];
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Static constructor to initialize static members of the <see cref="SolutionEntity"/> class.
        /// </summary>
        static SolutionEntity()
        {
            NameToIndexMap = new Dictionary<string, int>
            {
                {nameof(Id), 0},
                {nameof(Projects), 1}
            };

            IndexToObjectAccessMap = new Dictionary<int, Func<SolutionEntity, object?>>
            {
                {0, entity => entity.Id},
                {1, entity => entity.Projects}
            };
        }
        
        /// <summary>
        /// Gets the ID of the solution.
        /// </summary>
        public string Id => _solution.Id.Id.ToString();

        /// <summary>
        /// Gets the projects within the solution.
        /// </summary>
        [BindablePropertyAsTable]
        public IEnumerable<ProjectEntity> Projects
        {
            get
            {
                if (_wasLoaded) return _projects;

                _projects = _solution.Projects.Select(p => new ProjectEntity(p, _nuGetPackageMetadataRetriever, _cancellationToken)).ToArray();
                _wasLoaded = true;

                return _projects;
            }
        }
    
        /// <summary>
        /// Clones the solution entity with the specified NuGet package metadata retriever and cancellation token.
        /// </summary>
        /// <param name="nuGetPackageMetadataRetriever">The NuGet package metadata retriever.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A new instance of the <see cref="SolutionEntity"/> class.</returns>
        public SolutionEntity CloneWith(NuGetPackageMetadataRetriever nuGetPackageMetadataRetriever, CancellationToken cancellationToken)
        {
            return new SolutionEntity(_solution, nuGetPackageMetadataRetriever, cancellationToken);
        }
    }
}
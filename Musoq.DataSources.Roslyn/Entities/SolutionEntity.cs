using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
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

        internal readonly Solution Solution;
        
        private ProjectEntity[] _projects;
        private bool _wasLoaded;

        /// <summary>
        /// Initializes a new instance of the <see cref="SolutionEntity"/> class.
        /// </summary>
        /// <param name="solution">The solution.</param>
        public SolutionEntity(Solution solution)
        {
            Solution = solution;
            _projects = [];
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
        public string Id => Solution.Id.Id.ToString();

        /// <summary>
        /// Gets the projects within the solution.
        /// </summary>
        [BindablePropertyAsTable]
        public IEnumerable<ProjectEntity> Projects
        {
            get
            {
                if (_wasLoaded) return _projects;

                _projects = Solution.Projects.Select(p => new ProjectEntity(p)).ToArray();
                _wasLoaded = true;

                return _projects;
            }
        }
    }
}
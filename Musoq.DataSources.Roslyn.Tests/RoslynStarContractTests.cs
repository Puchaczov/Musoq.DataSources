using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Roslyn;
using Musoq.DataSources.Roslyn.Components.NuGet;
using Musoq.DataSources.Roslyn.Entities;
using Musoq.DataSources.Roslyn.Tests.Components;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins.Attributes;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public sealed class RoslynStarContractTests
{
    private static readonly StarContractCase[] Cases =
    [
        new(
            "solution",
            [typeof(string)],
            [RoslynTestPaths.SampleSolution],
            $"select * from csharp.solution('{RoslynTestPaths.SampleSolution.Escape()}')",
            [new StarContractColumn("Id", typeof(string))],
            ["Projects"])
    ];

    [TestMethod]
    public void Solution_HasExactPrimitiveStarContract()
    {
        var schema = new CSharpSchema();
        var context = CreateMetadataContext();

        StarContractAssertions.AssertConstructors(schema.GetRawConstructors(context), Cases);

        var table = schema.GetTableByName("solution", context, RoslynTestPaths.SampleSolution);
        StarContractAssertions.AssertExcludedColumnsRemainInSchema(table, Cases[0]);

        var projectsColumn = table.Columns.Single(column => column.ColumnName == "Projects");
        Assert.AreEqual(typeof(ProjectEntity[]), projectsColumn.ColumnType);

        var result = Compile(Cases[0].Query).Run();
        Assert.AreEqual(1, result.Count);
        StarContractAssertions.AssertResult(result, Cases[0]);
    }

    [TestMethod]
    public void SchemaCollectionProperties_HaveApplyMarkers()
    {
        var unmarked = typeof(SolutionEntity).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(SolutionEntity).Namespace && type.IsPublic)
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => IsApplyEligibleCollection(property.PropertyType))
                .Select(property => (Type: type, Property: property)))
            .Where(item => item.Property.GetCustomAttribute<BindablePropertyAsTableAttribute>(inherit: true) is null)
            .Select(item => $"{item.Type.Name}.{item.Property.Name}")
            .OrderBy(name => name)
            .ToArray();

        Assert.IsTrue(unmarked.Length == 0,
            $"Every schema-exposed Roslyn collection property must be apply-capable. Unmarked: {string.Join(", ", unmarked)}");
    }

    [TestMethod]
    public void CollectionOverrides_HaveDirectApplyMarkers()
    {
        var overrides = new[]
        {
            typeof(ClassEntity), typeof(EnumEntity), typeof(InterfaceEntity), typeof(StructEntity)
        };

        foreach (var type in overrides)
        foreach (var propertyName in new[] { nameof(TypeEntity.Methods), nameof(TypeEntity.Properties) })
        {
            var property = type.GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

            Assert.IsNotNull(property, $"{type.Name}.{propertyName} must be declared directly on the override.");
            Assert.IsNotNull(
                property!.GetCustomAttribute<BindablePropertyAsTableAttribute>(inherit: false),
                $"{type.Name}.{propertyName} must carry a direct BindablePropertyAsTable attribute.");
        }
    }

    [TestMethod]
    public void TypeCollectionOverrides_AreCrossApplyAddressable()
    {
        var queries = new Dictionary<string, (string Query, bool HasRows)>
        {
            ["class methods"] = (Query("cross apply d.Classes c cross apply c.Methods m where c.Name = 'Class1'", "m.Name"), true),
            ["class properties"] = (Query("cross apply d.Classes c cross apply c.Properties pr where c.Name = 'Class1'", "pr.Name"), true),
            ["enum methods"] = (Query("cross apply d.Enums e cross apply e.Methods m where e.Name = 'Enum1'", "m.Name"), false),
            ["enum properties"] = (Query("cross apply d.Enums e cross apply e.Properties ep where e.Name = 'Enum1'", "ep.Name"), false),
            ["interface methods"] = (Query("cross apply d.Interfaces i cross apply i.Methods m where i.Name = 'Interface1'", "m.Name"), true),
            ["interface properties"] = (Query("cross apply d.Interfaces i cross apply i.Properties ip where i.Name = 'IInterfaceWithMethods'", "ip.Name"), true),
            ["struct methods"] = (Query("cross apply d.Structs st cross apply st.Methods m where st.Name = 'StructWithPatterns'", "m.Name"), true),
            ["struct properties"] = (Query("cross apply d.Structs st cross apply st.Properties pr where st.Name = 'StructWithPatterns'", "pr.Name"), true)
        };

        foreach (var pair in queries)
        {
            try
            {
                var result = Compile(pair.Value.Query).Run();

                if (pair.Value.HasRows)
                    Assert.IsTrue(result.Count > 0, $"Roslyn apply '{pair.Key}' returned no rows.");
                else
                    Assert.AreEqual(0, result.Count, $"Roslyn apply '{pair.Key}' returned unexpected rows.");

                if (result.Count > 0)
                    Assert.AreEqual(typeof(string), result.Columns.Single().ColumnType);
            }
            catch (AssertFailedException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new AssertFailedException(
                    $"Roslyn apply '{pair.Key}' failed for query: {pair.Value.Query}", exception);
            }
        }
    }

    private static bool IsApplyEligibleCollection(Type type)
    {
        if (type == typeof(string))
            return false;

        if (type.IsArray)
            return true;

        return type.GetInterfaces()
            .Append(type)
            .Any(candidate => candidate.IsGenericType &&
                             candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    private static string Query(string suffix, string projection) =>
        $"select {projection} from csharp.solution('{RoslynTestPaths.SampleSolution.Escape()}') s " +
        $"cross apply s.Projects p cross apply p.Documents d {suffix}";

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            new RoslynSchemaProvider((_, _) => new Mock<INuGetPropertiesResolver>().Object),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables(
                new Dictionary<string, string>
                {
                    ["MUSOQ_SERVER_HTTP_ENDPOINT"] = "https://localhost/internal/this-doesnt-exists",
                    ["EXTERNAL_NUGET_PROPERTIES_RESOLVE_ENDPOINT"] = "https://localhost/external/this-doesnt-exists"
                }));
    }

    private static SourceMetadataContext CreateMetadataContext()
    {
        return new SourceMetadataContext(
            "roslyn-star-contract",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            NullLogger.Instance);
    }
}

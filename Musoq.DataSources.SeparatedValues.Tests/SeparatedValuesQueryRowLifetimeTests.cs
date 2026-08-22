#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SeparatedValuesQueryRowLifetimeTests
{
    [TestMethod]
    public void CompiledQuery_AfterStructAndClassCarrierRuns_DoesNotRetainCollectibleArtifacts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-query-unload-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, "Name,Age\nAda,36\nGrace,41\n", new UTF8Encoding(false, true));
        try
        {
            var source = $"separatedvalues.comma('{QueryPath(path)}', true, 0)";
            var references = CompileRunAndDispose(
                    $"select d.Name from {source} d where d.Age > 0",
                    "struct")
                .Concat(CompileRunAndDispose(
                    $"select l.Name, r.Name from {source} l inner join {source} r on l.Age = r.Age",
                    "class"))
                .ToArray();

            ForceCollection(references);

            foreach (var reference in references)
                Assert.IsFalse(reference.Reference.IsAlive, $"Generated {reference.Name} remained strongly reachable.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void QueryMaterializationTypes_HaveNoStaticGeneratedTypeRetentionSurface()
    {
        var assembly = typeof(SeparatedValuesSchema).Assembly;
        var queryPathTypes = assembly.GetTypes()
            .Where(static type => type.Namespace == typeof(SeparatedValuesSchema).Namespace)
            .Where(static type =>
                type.Name.Contains("Query", StringComparison.Ordinal) ||
                type == typeof(SeparatedValuesScanPipeline) ||
                type == typeof(SeparatedValuesParallelBlockScanPipeline))
            .ToArray();
        var violations = new List<string>();

        foreach (var type in queryPathTypes)
        foreach (var field in type.GetFields(
                     BindingFlags.Static |
                     BindingFlags.Public |
                     BindingFlags.NonPublic |
                     BindingFlags.DeclaredOnly))
        {
            if (field.IsLiteral)
                continue;
            if (CanRetainGeneratedState(field.FieldType))
                violations.Add($"{type.FullName}.{field.Name}: {field.FieldType}");
        }

        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IReadOnlyList<NamedWeakReference> CompileRunAndDispose(string query, string label)
    {
        using var compiled = InstanceCreatorHelpers.CompileForExecution(
            query,
            $"SeparatedValuesUnload_{label}_{Guid.NewGuid():N}",
            new CsvSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        using (var table = compiled.Run())
            Assert.IsTrue(table.Count > 0);

        var runtime = GetGeneratedRuntime(compiled);
        var assembly = runtime.Type.Assembly;
        var generatedTypes = assembly.GetTypes();
        var carriers = generatedTypes
            .Where(static type => type.Name.StartsWith("QueryRow_", StringComparison.Ordinal))
            .ToArray();
        var materializers = generatedTypes
            .Where(static type => type.Name.StartsWith("QueryRowMaterializer_", StringComparison.Ordinal))
            .ToArray();
        Assert.IsTrue(carriers.Length > 0);
        Assert.IsTrue(materializers.Length > 0);

        var references = new List<NamedWeakReference>
        {
            new($"{label} assembly load context", runtime.Context),
            new($"{label} assembly", assembly),
            new($"{label} runnable type", runtime.Type)
        };
        references.AddRange(carriers.Select((type, index) =>
            new NamedWeakReference($"{label} carrier {index}", type)));
        references.AddRange(materializers.Select((type, index) =>
            new NamedWeakReference($"{label} materializer {index}", type)));
        return references;
    }

    private static GeneratedRuntime GetGeneratedRuntime(CompiledQuery query)
    {
        var runnableField = typeof(CompiledQuery).GetField(
            "_runnable",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var current = runnableField?.GetValue(query) ??
                      throw new AssertFailedException("The compiled query did not retain a runnable.");
        while (FindProperty(current.GetType(), "Inner")?.GetValue(current) is { } inner)
            current = inner;

        var type = current.GetType();
        var context = AssemblyLoadContext.GetLoadContext(type.Assembly) ??
                      throw new AssertFailedException("The generated assembly has no load context.");
        Assert.IsTrue(context.IsCollectible);
        return new GeneratedRuntime(type, context);
    }

    private static PropertyInfo? FindProperty(Type type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.GetProperty(
                    name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is { } property)
                return property;
        }

        return null;
    }

    private static bool CanRetainGeneratedState(Type type)
    {
        if (type == typeof(object) || type == typeof(Type) || typeof(Delegate).IsAssignableFrom(type))
            return true;
        if (type.IsGenericParameter || type.ContainsGenericParameters)
            return true;
        if (!type.IsGenericType)
            return false;

        var definition = type.GetGenericTypeDefinition();
        if (definition == typeof(RowSource<>) || definition == typeof(IQueryRowMaterializer<>))
            return true;
        return type.GetGenericArguments().Any(CanRetainGeneratedState);
    }

    private static void ForceCollection(IReadOnlyList<NamedWeakReference> references)
    {
        for (var attempt = 0;
             attempt < 30 && references.Any(static reference => reference.Reference.IsAlive);
             attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Thread.Sleep(20);
        }
    }

    private static string QueryPath(string path)
    {
        return Path.GetFullPath(path)
            .Replace('\\', '/')
            .Replace("'", "''", StringComparison.Ordinal);
    }

    private sealed record GeneratedRuntime(Type Type, AssemblyLoadContext Context);

    private sealed class NamedWeakReference
    {
        public NamedWeakReference(string name, object target)
        {
            Name = name;
            Reference = new WeakReference(target);
        }

        public string Name { get; }

        public WeakReference Reference { get; }
    }
}

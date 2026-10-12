using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.RepresentativeTests;

[TestClass]
public sealed class NativeProviderArchitectureTests
{
    [TestMethod]
    public void ProductionProviders_ShouldNotRestoreErasedRowTransferOrGetterMaps()
    {
        var root = FindRoot();
        var violations = new List<string>();
        foreach (var directory in Directory.EnumerateDirectories(root, "Musoq.DataSources.*"))
        {
            var name = Path.GetFileName(directory);
            if (name.Contains("Tests", StringComparison.Ordinal) || name.Contains("Benchmark", StringComparison.Ordinal) ||
                name.Contains("Testing", StringComparison.Ordinal) || name.Contains("Playground", StringComparison.Ordinal))
                continue;
            foreach (var path in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                if (path.Split(Path.DirectorySeparatorChar).Any(static part => part is "obj" or "bin"))
                    continue;
                var source = File.ReadAllText(path);
                foreach (var forbidden in new[] { "RowSource<object", "IChunkWriter<object", "List<object>",
                             "LegacyJsonRecordProcessor", "LegacyJsonRowProcessorFactory", "RowType = typeof(object[])",
                             "new(typeof(object[]))", "DynamicInvoke(" })
                    if (source.Contains(forbidden, StringComparison.Ordinal))
                        violations.Add($"{Path.GetRelativePath(root, path)}: {forbidden}");
                if (Regex.IsMatch(source, @"Func<[^;\r\n]*,\s*object\??>"))
                    violations.Add($"{Path.GetRelativePath(root, path)}: object-returning row getter");
            }
        }
        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Musoq.DataSources.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("DataSources solution root was not found.");
    }
}

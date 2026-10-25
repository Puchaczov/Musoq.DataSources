using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Musoq.Schema.Optimization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
namespace Musoq.DataSources.Os.Tests;
[TestClass]
[DoNotParallelize]
public sealed class NativePatternTests
{
    [TestMethod]
    public void ProcessPatterns_UseOnlyStableIdentityGetters()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var row = new Process.ProcessEntity(process);
        var schema = new OsSchema();
        var identity = new SourceIdentity("os", "p", "p", "processes");
        var context = RuntimeV2TestContexts.CreateExecutionContext(executionPlan: SourceExecutionPlan.Empty(identity));
        var descriptor = schema.DescribeSource("processes", new(identity, context));
        CollectionAssert.AreEquivalent(new[] { "MachineName", "ProcessName" }, Enumerable.ToArray(Enumerable.Select(descriptor.PredicateCapabilities.StringMatches, item => item.Column.Name)));
        foreach (var name in new[] { "MachineName", "ProcessName" })
        foreach (var kind in Enum.GetValues<SourceStringMatchKind>().Select(value => (SourceStringMatchKind?)value).Append(null))
        foreach (var negated in new[] { false, true })
        {
            var pattern = kind switch { SourceStringMatchKind.Exact => "t", SourceStringMatchKind.Prefix => "t%", SourceStringMatchKind.Suffix => "%t", _ => "%t%" };
            SourcePredicatePatternMatch predicate = kind is null
                ? new SourcePredicateRegexMatch(new(name), "t", TimeSpan.FromMilliseconds(17), isNegated: negated)
                : new SourcePredicateStringMatch(new(name), kind.Value, pattern, "t", isNegated: negated);
            var result = schema.TryPlanSource("processes", new() { Identity = identity, Predicate = predicate });
            var execution = RuntimeV2TestContexts.CreateExecutionContext(executionPlan: result.ExecutionPlan);
            var source = Common.PatternSourcePlan.Read(execution, _ => new ProcessRows(row));
            var count = Enumerable.Count(Enumerable.SelectMany(source.Chunks, chunk => chunk));
            var value = name == "ProcessName" ? row.ProcessName : row.MachineName;
            Assert.AreEqual(((kind is null ? Evaluator.Operators.RLikeDynamic(value, "t") : Evaluator.Operators.LikePrepared(value, Evaluator.Operators.PrepareLike(pattern))) != negated) ? 1 : 0, count);
        }
    }

    private sealed class ProcessRows(Process.ProcessEntity row) : Schema.DataSources.RowSource<Process.ProcessEntity>
    {
        public override System.Collections.Generic.IEnumerable<System.Collections.Generic.IReadOnlyList<Process.ProcessEntity>> Chunks
        {
            get { yield return new[] { row }; }
        }
    }

    [TestMethod]
    public void AllStableOsStringColumns_MatchCore()
    {
        var root = Path.Combine(Path.GetTempPath(), "musoq-pattern-os-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "Alpha.txt"); File.WriteAllText(file, "Alpha");
            var sub = Path.Combine(root, "Alpha"); Directory.CreateDirectory(sub); File.WriteAllText(Path.Combine(sub, "Beta.txt"), "Beta");
            var zip = Path.Combine(root, "Alpha.zip"); using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create)) { using var writer = new StreamWriter(archive.CreateEntry("Alpha.txt").Open()); writer.Write("Alpha"); }
            var png = Path.Combine(root, "Alpha.png");
            File.WriteAllBytes(png, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9e8AAAAASUVORK5CYII="));
            var schema = new OsSchema();
            PatternContractVerifier.For<Files.FileEntity>().Verify(schema, "file", SourceArguments.Create(file, SourceArguments.Empty), [file]);
            PatternContractVerifier.For<Files.FileEntity>().Verify(schema, "files", SourceArguments.Create(root, SourceArguments.Create(false, SourceArguments.Empty)), [root, false]);
            PatternContractVerifier.For<Directories.DirectoryEntity>().Verify(schema, "directories", SourceArguments.Create(root, SourceArguments.Create(false, SourceArguments.Empty)), [root, false]);
            PatternContractVerifier.For<Zip.ZipEntryEntity>().Verify(schema, "zip", SourceArguments.Create(zip, SourceArguments.Empty), [zip]);
            PatternContractVerifier.For<Compare.Directories.CompareDirectoriesResult>().Verify(schema, "dirscompare", SourceArguments.Create(root, SourceArguments.Create(sub, SourceArguments.Empty)), [root, sub]);
            PatternContractVerifier.For<Runtime.CultureEntity>().Verify(schema, "cultures", SourceArguments.Empty, []);
            PatternContractVerifier.For<Runtime.CurrentCultureEntity>().Verify(schema, "currentculture", SourceArguments.Empty, []);
            PatternContractVerifier.For<Runtime.EncodingEntity>().Verify(schema, "encodings", SourceArguments.Empty, []);
            PatternContractVerifier.For<Runtime.TimeZoneEntity>().Verify(schema, "timezones", SourceArguments.Empty, []);
            PatternContractVerifier.For<Runtime.RuntimeEntity>().Verify(schema, "runtime", SourceArguments.Empty, []);
            PatternContractVerifier.For<Runtime.DriveEntity>().Verify(schema, "drives", SourceArguments.Empty, []);
            PatternContractVerifier.For<Runtime.SpecialFolderEntity>().Verify(schema, "specialfolders", SourceArguments.Empty, []);
            PatternContractVerifier.For<Runtime.FileAttributeEntity>().Verify(schema, "fileattributes", SourceArguments.Empty, []);
            PatternContractVerifier.For<Runtime.EnvironmentVariableEntity>().Verify(schema, "environmentvariables", SourceArguments.Empty, []);
            PatternContractVerifier.For<Runtime.PathInfoEntity>().Verify(schema, "pathinfo", SourceArguments.Create(file, SourceArguments.Empty), [file]);
            PatternContractVerifier.For<Metadata.MetadataEntity>().Verify(schema, "metadata", SourceArguments.Create(png, SourceArguments.Empty), [png]);
        }
        finally { Directory.Delete(root, true); }
    }
}

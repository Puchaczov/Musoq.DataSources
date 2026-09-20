using System.Collections.Generic;
using System.IO;
using Musoq.DataSources.Os.Dlls;
using Musoq.DataSources.Os.Files;
using Musoq.DataSources.Os.Metadata;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Tests.Utils;

internal static class PredicatePushdownTestHelpers
{
    public static SourcePlanResult Plan(
        string sourceName,
        SourcePredicateExpression? predicate)
    {
        return new OsSchema().TryPlanSource(
            sourceName,
            new SourcePlanRequest
            {
                Identity = new SourceIdentity("os", sourceName, sourceName, sourceName),
                RequiredColumns = [],
                SourceRuntimeSettings = new Dictionary<string, string>(),
                Predicate = predicate,
                OrderBy = []
            });
    }

    public static SourceExecutionContext CreateContext(
        SourcePlanResult plan,
        DataSourceEventHandler? progressCallback = null)
    {
        return RuntimeV2TestContexts.CreateExecutionContext(
            executionPlan: plan.ExecutionPlan,
            dataSourceProgressCallback: progressCallback);
    }

    public static SourcePredicateComparison Equal(string columnName, string value)
    {
        return Compare(SourcePredicateComparisonOperator.Equal, columnName, value);
    }

    public static SourcePredicateComparison Compare(
        SourcePredicateComparisonOperator op,
        string columnName,
        object value)
    {
        return new SourcePredicateComparison(
            op,
            new SourcePredicateColumn(new SourceColumnRef(columnName)),
            new SourcePredicateLiteral(value));
    }

    public static long RowsRead(
        DataSourceProgressCapture capture,
        string sourceName)
    {
        long rowsRead = 0;
        foreach (var progress in capture.For(sourceName, DataSourcePhase.RowsRead))
            rowsRead += progress.RowsProcessed ?? 0;

        return rowsRead;
    }

    public static long RowsEmitted(
        DataSourceProgressCapture capture,
        string sourceName)
    {
        foreach (var progress in capture.For(sourceName, DataSourcePhase.End))
            return progress.RowsProcessed ?? 0;

        return 0;
    }

    public static string QueryPath(string path)
    {
        return path.Replace('\\', '/');
    }
}

internal sealed class PredicateCountingFilesSource(
    string path,
    bool useSubDirectories,
    SourceExecutionContext executionContext)
    : FilesSource(path, useSubDirectories, executionContext)
{
    public long FilesEnumerated { get; private set; }

    public long EntitiesMaterialized { get; private set; }

    protected override IEnumerable<FileInfo> GetFiles(DirectoryInfo directoryInfo)
    {
        foreach (var file in base.GetFiles(directoryInfo))
        {
            FilesEnumerated++;
            yield return file;
        }
    }

    protected override FileEntity CreateBasedOnFile(FileInfo file, string rootDirectory)
    {
        EntitiesMaterialized++;
        return base.CreateBasedOnFile(file, rootDirectory);
    }
}

internal sealed class PredicateCountingDllSource(
    string path,
    bool useSubDirectories,
    SourceExecutionContext executionContext)
    : DllSource(path, useSubDirectories, executionContext)
{
    public long FilesEnumerated { get; private set; }

    public long EntitiesMaterialized { get; private set; }

    protected override IEnumerable<FileInfo> GetFiles(DirectoryInfo directoryInfo)
    {
        foreach (var file in base.GetFiles(directoryInfo))
        {
            FilesEnumerated++;
            yield return file;
        }
    }

    protected override DllInfo CreateBasedOnFile(FileInfo file, string rootDirectory)
    {
        EntitiesMaterialized++;
        return new DllInfo { FileInfo = file };
    }
}

internal sealed class PredicateCountingMetadataSource : MetadataSource
{
    public PredicateCountingMetadataSource(
        string directoryPath,
        SourceExecutionContext executionContext)
        : base(
            directoryPath,
            null,
            false,
            PathType.MustBeDirectory,
            false,
            executionContext)
    {
    }

    public long FilesEnumerated { get; private set; }

    public long EntitiesMaterialized { get; private set; }

    protected override IEnumerable<FileInfo> GetFiles(DirectoryInfo directoryInfo)
    {
        foreach (var file in base.GetFiles(directoryInfo))
        {
            FilesEnumerated++;
            yield return file;
        }
    }

    protected override void ProcessFile(
        FileInfo file,
        DirectorySourceSearchOptions source,
        List<MetadataEntity> dirFiles)
    {
        EntitiesMaterialized++;
        base.ProcessFile(file, source, dirFiles);
    }
}

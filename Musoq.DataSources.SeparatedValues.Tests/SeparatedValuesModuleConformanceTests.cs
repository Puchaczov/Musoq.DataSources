#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Musoq.DataSources.Structured;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
using Musoq.Schema.DataSources;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class SeparatedValuesModuleConformanceTests
{
    [TestMethod]
    public void SchemaResolver_CanBeExchangedWithoutChangingTheSchemaFacade()
    {
        var resolver = new CapturingSchemaResolver(CreateContract());
        var schema = new SeparatedValuesSchema(new SeparatedValuesPipelineModules(
            resolver,
            new CapturingScanPipeline()));
        var metadataContext = new SourceMetadataContext(
            "module-test",
            CancellationToken.None,
            [],
            new Dictionary<string, string>(),
            new Mock<ILogger>().Object);

        var table = schema.GetTableByName("comma", metadataContext, "unused.csv", true, 0);

        Assert.AreEqual(1, resolver.Calls);
        Assert.AreEqual("Name", table.Columns.Single().ColumnName);
        Assert.AreEqual(typeof(string), table.Columns.Single().ColumnType);
    }

    [TestMethod]
    public void ScanPipeline_CanBeExchangedWithoutChangingTheRowSourceFacade()
    {
        var pipeline = new CapturingScanPipeline();
        var context = RuntimeV2TestContexts.CreateExecutionContext(
            executionPlan: new SourceExecutionPlan
            {
                Identity = SourceIdentity.Empty,
                AcceptedColumns = [],
                AcceptedOrderBy = []
            });
        var source = new SeparatedValuesFromFileRowsSource(
            "unused.csv",
            ",",
            true,
            0,
            context,
            pipeline);

        var rows = source.Chunks.SelectMany(chunk => chunk).ToArray();

        Assert.AreEqual(1, pipeline.Calls);
        Assert.AreEqual("unused.csv", System.IO.Path.GetFileName(pipeline.LastRequest.Path));
        Assert.AreEqual("module-row", rows.Single()[0]);
    }

    private static SeparatedValuesSourceContract CreateContract()
    {
        var identity = new StructuredFileIdentity("unused.csv", 0, 0, "module-test", default);
        var snapshot = new StructuredSchemaSnapshot(
            identity,
            [new StructuredColumnSnapshot("Name", 0, new StructuredTypeState(StructuredValueKind.String, false), 0)],
            0);
        return new SeparatedValuesSourceContract(
            snapshot,
            SeparatedValuesSchemaResolutionMode.Declared,
            true,
            0,
            0,
            TimeSpan.Zero);
    }

    private sealed class CapturingSchemaResolver(SeparatedValuesSourceContract contract)
        : ISeparatedValuesSchemaResolver
    {
        public int Calls { get; private set; }

        public SeparatedValuesSourceContract Resolve(SeparatedValuesSchemaResolutionRequest request)
        {
            Calls++;
            return contract;
        }
    }

    private sealed class CapturingScanPipeline : ISeparatedValuesScanPipeline
    {
        public int Calls { get; private set; }

        public SeparatedValuesScanRequest LastRequest { get; private set; }

        public void Execute(SeparatedValuesScanRequest request, IChunkWriter<object?[]> writer)
        {
            Calls++;
            LastRequest = request;
            writer.Write([new object?[] { "module-row" }]);
        }
    }
}

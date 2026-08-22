#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
public class SeparatedValuesParallelBlockPipelineTests
{
    [TestMethod]
    public void TinyBlocks_WithQuotedMultilineRecords_MatchSequentialOrderAndValues()
    {
        const string contents = "A,B,C\r\n" +
                                "1,\"alpha,beta\",\"line one\r\nline \"\"two\"\"\"\r\n" +
                                "2,plain,\"\"\n" +
                                "\n" +
                                "3,\"quoted\",tail\n";
        WithCsv(contents, path =>
        {
            var plan = Plan(path);
            var sequential = ReadRows(path, plan, "1", null);

            for (var blockSize = 1; blockSize <= 64; blockSize++)
            {
                var analyzer = new CountingAnalyzer();
                var scan = new SeparatedValuesScanPipeline(
                    new SeparatedValuesParallelBlockScanPipeline(
                        boundaryAnalyzer: analyzer,
                        blockSize: blockSize),
                    forceParallel: true);

                var parallel = ReadRows(path, plan, "4", scan);

                Assert.AreEqual(sequential.Length, parallel.Length, $"blockSize={blockSize}");
                for (var row = 0; row < sequential.Length; row++)
                    Assert.AreEqual(sequential[row], parallel[row], $"blockSize={blockSize}, row={row}");
                Assert.IsTrue(analyzer.Calls > 1, $"blockSize={blockSize}");
            }
        });
    }

    [TestMethod]
    public void TinyBlocks_WithCustomQuoteAndTrimmedFields_MatchSequentialOrderAndValues()
    {
        const string contents = "A;B\n" +
                                " 1 ; 'alpha;''beta'\n" +
                                " 2 ; plain \n" +
                                " 3 ; 'line one\nline two'\n";
        WithCsv(contents, path =>
        {
            var settings = new Dictionary<string, string>
            {
                [SeparatedValuesInferenceOptions.MaximumTimeMillisecondsSettingName] = "1000",
                [SeparatedValuesDialect.QuoteSettingName] = "'",
                [SeparatedValuesDialect.WhitespaceSettingName] = "trim"
            };
            var request = new SourcePlanRequest
            {
                Identity = SourceIdentity.Empty,
                RequiredColumns = [new SourceColumnRef("A"), new SourceColumnRef("B")],
                SourceRuntimeSettings = settings,
                Predicate = null,
                OrderBy = [],
                Skip = null,
                Take = null
            };
            var plan = new SeparatedValuesSchema()
                .TryPlanSource("semicolon", request, path, true, 0)
                .ExecutionPlan;

            var sequential = ReadConfiguredRows(path, plan, settings, "1");
            foreach (var blockSize in new[] { 1, 17, 32 })
            {
                var parallelSettings = new Dictionary<string, string>(settings)
                {
                    [SeparatedValuesParallelScanOptions.MaximumParallelismSettingName] = "4"
                };
                var context = RuntimeV2TestContexts.CreateExecutionContext(
                    allColumns:
                    [
                        new SchemaColumn("A", 0, typeof(string)),
                        new SchemaColumn("B", 1, typeof(string))
                    ],
                    sourceRuntimeSettings: parallelSettings,
                    executionPlan: plan);
                var pipeline = new SeparatedValuesScanPipeline(
                    new SeparatedValuesParallelBlockScanPipeline(blockSize: blockSize),
                    forceParallel: true);
                var source = SeparatedValuesNativeTestSource.Create<string, string>(
                    path,
                    ";",
                    true,
                    0,
                    context,
                    pipeline);
                var parallel = source.Chunks.SelectMany(chunk => chunk).ToArray();

                Assert.AreEqual(sequential.Length, parallel.Length, $"blockSize={blockSize}");
                for (var row = 0; row < sequential.Length; row++)
                    Assert.AreEqual(sequential[row], parallel[row], $"blockSize={blockSize}, row={row}");
            }
        });
    }

    [TestMethod]
    public void ClassifiedUnquotedProjectedKernel_MatchesSequentialForAsciiDelimitersAndLineEndings()
    {
        foreach (var separator in new[] { ",", ";", "\t", "|" })
        {
            foreach (var ending in new[] { "\n", "\r\n" })
            {
                var contents = string.Join(
                    ending,
                    $"A{separator}B{separator}C",
                    $"1{separator}alpha{separator}ten",
                    $"2{separator}{separator}twenty",
                    $"3{separator}omega",
                    $"4{separator}café{separator}forty",
                    string.Empty);
                WithCsv(contents, path =>
                {
                    var plan = Plan(path, separator);
                    var sequential = ReadRows(path, plan, "1", null, separator);

                    foreach (var blockSize in new[] { 1, 17, 32 })
                    {
                        var analyzer = new ClassifyingAnalyzer();
                        var scan = new SeparatedValuesScanPipeline(
                            new SeparatedValuesParallelBlockScanPipeline(
                                boundaryAnalyzer: analyzer,
                                blockSize: blockSize),
                            forceParallel: true);
                        var parallel = ReadRows(path, plan, "4", scan, separator);

                        Assert.AreEqual(sequential.Length, parallel.Length,
                            $"separator={separator}, ending={Escape(ending)}, blockSize={blockSize}");
                        for (var row = 0; row < sequential.Length; row++)
                        {
                            Assert.AreEqual(
                                sequential[row],
                                parallel[row],
                                $"separator={separator}, ending={Escape(ending)}, blockSize={blockSize}, row={row}");
                        }

                        Assert.IsTrue(
                            analyzer.CompactAnalyses > 0,
                            $"separator={separator}, ending={Escape(ending)}, blockSize={blockSize}");
                    }
                });
            }
        }
    }

    [TestMethod]
    public void ClassifiedUnquotedProjectedKernel_RejectsInvalidUtf8()
    {
        WithBytes([.. "A,B\n1,valid\n2,"u8, 0xff, (byte)'\n'], path =>
        {
            var plan = DeclaredProjectedPlan(path);
            var analyzer = new ClassifyingAnalyzer();
            var scan = new SeparatedValuesScanPipeline(
                new SeparatedValuesParallelBlockScanPipeline(
                    boundaryAnalyzer: analyzer,
                    blockSize: 64),
                forceParallel: true);
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                allColumns:
                [
                    new SchemaColumn("A", 0, typeof(string)),
                    new SchemaColumn("B", 1, typeof(string))
                ],
                sourceRuntimeSettings: new Dictionary<string, string>
                {
                    [SeparatedValuesParallelScanOptions.MaximumParallelismSettingName] = "4"
                },
                executionPlan: plan);
            var source = SeparatedValuesNativeTestSource.Create<string, string>(
                path, ",", true, 0, context, scan);

            Assert.ThrowsExactly<InvalidDataException>(() => source.Chunks.ToArray());
            Assert.IsTrue(analyzer.CompactAnalyses > 0);
        });
    }

    [TestMethod]
    public void OutputBudget_AllowsOneVeryLongProjectedRecordToRunExclusively()
    {
        var contents = "A,B\n1," + new string('x', 200_000) + "\n";
        WithCsv(contents, path =>
        {
            var plan = DeclaredProjectedPlan(path);
            var budget = new SeparatedValuesOutputMemoryBudget(64 * 1024, 1024);
            var scan = new SeparatedValuesScanPipeline(
                new SeparatedValuesParallelBlockScanPipeline(
                    blockSize: 64 * 1024,
                    outputMemoryBudget: budget),
                forceParallel: true);
            var context = RuntimeV2TestContexts.CreateExecutionContext(
                allColumns:
                [
                    new SchemaColumn("A", 0, typeof(string)),
                    new SchemaColumn("B", 1, typeof(string))
                ],
                sourceRuntimeSettings: new Dictionary<string, string>
                {
                    [SeparatedValuesParallelScanOptions.MaximumParallelismSettingName] = "4"
                },
                executionPlan: plan);
            var rows = SeparatedValuesNativeTestSource.Create<string, string>(
                    path, ",", true, 0, context, scan)
                .Chunks
                .SelectMany(chunk => chunk)
                .ToArray();

            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual(200_000, ((string)rows[0][1]!).Length);
            Assert.AreEqual(0L, budget.CurrentReservedBytes);
            Assert.AreEqual(1L, budget.OversizedReservationCount);
            Assert.IsTrue(budget.LargestOversizedRequestBytes > budget.CapacityBytes);
        });
    }

    [TestMethod]
    public void BlockReader_KeepsMultipleRandomAccessReadsOutstanding()
    {
        var builder = new StringBuilder("A,B,C\n");
        for (var row = 0; row < 100; row++)
            builder.Append(row).Append(',').Append("value").Append(',').Append(row + 1).Append('\n');

        WithCsv(builder.ToString(), path =>
        {
            var plan = Plan(path);
            var factory = new TrackingBlockSourceFactory(File.ReadAllBytes(path));
            var scan = new SeparatedValuesScanPipeline(
                new SeparatedValuesParallelBlockScanPipeline(
                    factory,
                    blockSize: 32),
                forceParallel: true);

            var rows = ReadRows(path, plan, "4", scan);

            Assert.AreEqual(100, rows.Length);
            Assert.IsTrue(factory.MaximumConcurrentReads > 1, $"max={factory.MaximumConcurrentReads}");
        });
    }

    [TestMethod]
    public void BlockReader_IoDepthIsIndependentFromCpuWorkerCount()
    {
        var builder = new StringBuilder("A,B,C\n");
        for (var row = 0; row < 200; row++)
            builder.Append(row).Append(',').Append("value").Append(',').Append(row + 1).Append('\n');

        WithCsv(builder.ToString(), path =>
        {
            var plan = Plan(path);
            var factory = new TrackingBlockSourceFactory(File.ReadAllBytes(path));
            var scan = new SeparatedValuesScanPipeline(
                new SeparatedValuesParallelBlockScanPipeline(
                    factory,
                    blockSize: 32,
                    ioDepth: 8),
                forceParallel: true);

            var rows = ReadRows(path, plan, "2", scan);

            Assert.AreEqual(200, rows.Length);
            Assert.IsTrue(factory.MaximumConcurrentReads > 2, $"max={factory.MaximumConcurrentReads}");
        });
    }

    [TestMethod]
    public async Task BlockReader_DrainsFinalizedWorkBeforeAwaitingBackpressuredRead()
    {
        var builder = new StringBuilder("A,B,C\n");
        for (var row = 0; row < 200; row++)
            builder.Append(row).Append(',').Append("value").Append(',').Append(row + 1).Append('\n');

        var path = Path.Combine(Path.GetTempPath(), $"musoq-csv-blocks-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false, true));
        var releaseBlockedRead = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var plan = Plan(path);
            var factory = new TrackingBlockSourceFactory(
                File.ReadAllBytes(path),
                releaseBlockedRead.Task,
                blockedSequence: 4);
            var budget = new SignalingOutputMemoryBudget(releaseBlockedRead);
            var scan = new SeparatedValuesScanPipeline(
                new SeparatedValuesParallelBlockScanPipeline(
                    factory,
                    blockSize: 32,
                    outputMemoryBudget: budget,
                    ioDepth: 8,
                    yieldBeforeCpuWork: false),
                forceParallel: true);

            var readTask = Task.Run(() => ReadRows(path, plan, "2", scan));
            TestRow2<long?, string>[] rows;
            try
            {
                rows = await readTask.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                releaseBlockedRead.TrySetResult(true);
                await readTask.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Fail("The producer waited for a backpressured read before draining finalized work.");
                return;
            }

            Assert.AreEqual(200, rows.Length);
            Assert.IsTrue(factory.MaximumConcurrentReads > 1, $"max={factory.MaximumConcurrentReads}");
        }
        finally
        {
            releaseBlockedRead.TrySetResult(true);
            File.Delete(path);
        }
    }

    [TestMethod]
    public void DeclaredZeroColumnKernel_CountsStrictGrammarAcrossCompactAndQuotedBlocks()
    {
        const string contents = "A,B\n" +
                                "1,2\n\n3,4\n5,6\n7,8\n9,10\n" +
                                "11,\"line one\nline two\"\n13,14\n" +
                                ",left-empty\nright-empty,\n,\n";
        WithCsv(contents, path =>
        {
            var plan = DeclaredZeroColumnPlan(path);
            for (var blockSize = 1; blockSize <= 96; blockSize++)
            {
                var scan = new SeparatedValuesScanPipeline(
                    new SeparatedValuesParallelBlockScanPipeline(blockSize: blockSize),
                    forceParallel: true);

                Assert.AreEqual(10L, ReadZeroColumnCount(path, plan, scan), $"blockSize={blockSize}");
            }
        });
    }

    [TestMethod]
    public void DeclaredZeroColumnKernel_RejectsMalformedConsumedRecords()
    {
        string[] malformed =
        [
            "A,B\n1,2\n3,4,5\n",
            "A,B\n1,2\n3\r4,5\n",
            "A,B\n1,2\n3,ab\"cd\n",
            "A,B\n1,2\n3,\"closed\"tail\n"
        ];

        foreach (var contents in malformed)
        {
            WithCsv(contents, path =>
            {
                var plan = DeclaredZeroColumnPlan(path);
                for (var blockSize = 1; blockSize <= 96; blockSize++)
                {
                    var scan = new SeparatedValuesScanPipeline(
                        new SeparatedValuesParallelBlockScanPipeline(blockSize: blockSize),
                        forceParallel: true);

                    if (contents.Contains("3,4,5", StringComparison.Ordinal))
                    {
                        Assert.ThrowsExactly<StructuredSchemaDriftException>(
                            () => ReadZeroColumnCount(path, plan, scan),
                            $"blockSize={blockSize}");
                    }
                    else
                    {
                        Assert.ThrowsExactly<InvalidDataException>(
                            () => ReadZeroColumnCount(path, plan, scan),
                            $"blockSize={blockSize}");
                    }
                }
            });
        }
    }

    [TestMethod]
    public void DeclaredZeroColumnKernel_RejectsInvalidUtf8OutsideMetadataPrefix()
    {
        WithBytes([.. "A,B\n1,2\n3,"u8, 0xff, (byte)'\n'], path =>
        {
            var plan = DeclaredZeroColumnPlan(path);
            var scan = new SeparatedValuesScanPipeline(
                new SeparatedValuesParallelBlockScanPipeline(blockSize: 64),
                forceParallel: true);

            Assert.ThrowsExactly<InvalidDataException>(() => ReadZeroColumnCount(path, plan, scan));
        });
    }

    [TestMethod]
    public void ParallelFailures_AreReportedInSourceOrder()
    {
        const int blockSize = 64 * 1024;
        var contents = new StringBuilder("A,B\n");
        contents.Append('"')
            .Append('x', blockSize - 64)
            .Append("\",1\n")
            .Append("early,b,extra\n")
            .Append('y', 128)
            .Append(",1\n")
            .Append("late,b,extra\n");

        WithCsv(contents.ToString(), path =>
        {
            var plan = DeclaredZeroColumnPlan(path);
            var scan = new SeparatedValuesScanPipeline(
                new SeparatedValuesParallelBlockScanPipeline(blockSize: blockSize),
                forceParallel: true);

            var exception = Assert.ThrowsExactly<StructuredSchemaDriftException>(() =>
                ReadZeroColumnCount(path, plan, scan));

            StringAssert.Contains(exception.Message, "row 2");
        });
    }

    private static SourceExecutionPlan Plan(string path)
    {
        return Plan(path, ",");
    }

    private static SourceExecutionPlan Plan(string path, string separator)
    {
        var request = new SourcePlanRequest
        {
            Identity = SourceIdentity.Empty,
            RequiredColumns = [new SourceColumnRef("A"), new SourceColumnRef("C")],
            SourceRuntimeSettings = new Dictionary<string, string>
            {
                [SeparatedValuesInferenceOptions.MaximumTimeMillisecondsSettingName] = "1000"
            },
            Predicate = null,
            OrderBy = [],
            Skip = null,
            Take = null
        };
        var schema = new SeparatedValuesSchema();
        return separator switch
        {
            "," => schema.TryPlanSource("comma", request, path, true, 0).ExecutionPlan,
            ";" => schema.TryPlanSource("semicolon", request, path, true, 0).ExecutionPlan,
            "\t" => schema.TryPlanSource("tab", request, path, true, 0).ExecutionPlan,
            _ => schema.TryPlanSource("delimited", request, path, separator, true, 0).ExecutionPlan
        };
    }

    private static SourceExecutionPlan DeclaredProjectedPlan(string path)
    {
        var settings = new Dictionary<string, string>
        {
            [SeparatedValuesInferenceOptions.MaximumTimeMillisecondsSettingName] = "1000"
        };
        var schema = new SeparatedValuesSchema();
        var metadata = new SourceMetadataContext(
            "declared-projected-test",
            CancellationToken.None,
            [
                new SchemaColumn("A", 0, typeof(string)),
                new SchemaColumn("B", 1, typeof(string))
            ],
            settings,
            new Mock<ILogger>().Object);
        _ = schema.DescribeSource(
            "comma",
            new SourceDescribeContext(SourceIdentity.Empty, metadata),
            path,
            true,
            0);
        return schema.TryPlanSource(
                "comma",
                new SourcePlanRequest
                {
                    Identity = SourceIdentity.Empty,
                    RequiredColumns = [new SourceColumnRef("A"), new SourceColumnRef("B")],
                    SourceRuntimeSettings = settings,
                    Predicate = null,
                    OrderBy = [],
                    Skip = null,
                    Take = null
                },
                path,
                true,
                0)
            .ExecutionPlan;
    }

    private static SourceExecutionPlan DeclaredZeroColumnPlan(string path)
    {
        var settings = new Dictionary<string, string>
        {
            [SeparatedValuesInferenceOptions.MaximumTimeMillisecondsSettingName] = "1000"
        };
        var schema = new SeparatedValuesSchema();
        var metadata = new SourceMetadataContext(
            "declared-zero-column-test",
            CancellationToken.None,
            [
                new SchemaColumn("A", 0, typeof(string)),
                new SchemaColumn("B", 1, typeof(string))
            ],
            settings,
            new Mock<ILogger>().Object);
        _ = schema.DescribeSource(
            "comma",
            new SourceDescribeContext(SourceIdentity.Empty, metadata),
            path,
            true,
            0);
        return schema.TryPlanSource(
                "comma",
                new SourcePlanRequest
                {
                    Identity = SourceIdentity.Empty,
                    RequiredColumns = [],
                    SourceRuntimeSettings = settings,
                    Predicate = null,
                    OrderBy = [],
                    Skip = null,
                    Take = null
                },
                path,
                true,
                0)
            .ExecutionPlan;
    }

    private static long ReadZeroColumnCount(
        string path,
        SourceExecutionPlan plan,
        ISeparatedValuesQueryScanPipeline pipeline)
    {
        var context = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns: [],
            sourceRuntimeSettings: new Dictionary<string, string>
            {
                [SeparatedValuesParallelScanOptions.MaximumParallelismSettingName] = "4"
            },
            executionPlan: plan);
        var source = SeparatedValuesNativeTestSource.Create(path, ",", true, 0, context, pipeline);
        return source.Chunks.Sum(chunk => (long)chunk.Count);
    }

    private static TestRow2<long?, string>[] ReadRows(
        string path,
        SourceExecutionPlan plan,
        string parallelism,
        ISeparatedValuesQueryScanPipeline? pipeline,
        string separator = ",")
    {
        var context = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns:
            [
                new SchemaColumn("A", 0, typeof(long?)),
                new SchemaColumn("C", 1, typeof(string))
            ],
            sourceRuntimeSettings: new Dictionary<string, string>
            {
                [SeparatedValuesParallelScanOptions.MaximumParallelismSettingName] = parallelism
            },
            executionPlan: plan);
        var source = SeparatedValuesNativeTestSource.Create<long?, string>(
            path, separator, true, 0, context, pipeline);
        return source.Chunks.SelectMany(chunk => chunk).ToArray();
    }

    private static string Escape(string value)
    {
        return value.Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static TestRow2<string, string>[] ReadConfiguredRows(
        string path,
        SourceExecutionPlan plan,
        IReadOnlyDictionary<string, string> settings,
        string parallelism)
    {
        var executionSettings = new Dictionary<string, string>(settings)
        {
            [SeparatedValuesParallelScanOptions.MaximumParallelismSettingName] = parallelism
        };
        var context = RuntimeV2TestContexts.CreateExecutionContext(
            allColumns:
            [
                new SchemaColumn("A", 0, typeof(string)),
                new SchemaColumn("B", 1, typeof(string))
            ],
            sourceRuntimeSettings: executionSettings,
            executionPlan: plan);
        var source = SeparatedValuesNativeTestSource.Create<string, string>(
            path,
            ";",
            true,
            0,
            context);
        return source.Chunks.SelectMany(chunk => chunk).ToArray();
    }

    private static void WithCsv(string contents, Action<string> assertion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-csv-blocks-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, contents, new UTF8Encoding(false, true));
        try
        {
            assertion(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void WithBytes(byte[] contents, Action<string> assertion)
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-csv-blocks-{Guid.NewGuid():N}.csv");
        File.WriteAllBytes(path, contents);
        try
        {
            assertion(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class CountingAnalyzer : ISeparatedValuesRecordBoundaryAnalyzer
    {
        private readonly QuoteParitySeparatedValuesRecordBoundaryAnalyzer _inner = new();

        public int Calls => Volatile.Read(ref _calls);

        public SeparatedValuesBlockAnalysis Analyze(SeparatedValuesByteBlock block)
        {
            Interlocked.Increment(ref _calls);
            return _inner.Analyze(block);
        }

        public SeparatedValuesBlockAnalysis Analyze(
            SeparatedValuesByteBlock block,
            SeparatedValuesDialect dialect)
        {
            Interlocked.Increment(ref _calls);
            return _inner.Analyze(block, dialect);
        }

        public SeparatedValuesBlockAnalysis AnalyzeFraming(
            SeparatedValuesByteBlock block,
            SeparatedValuesFramingAnalysisOptions options,
            SeparatedValuesDialect dialect)
        {
            Interlocked.Increment(ref _calls);
            return _inner.AnalyzeFraming(block, options, dialect);
        }

        private int _calls;
    }

    private sealed class ClassifyingAnalyzer : ISeparatedValuesRecordBoundaryAnalyzer
    {
        private readonly QuoteParitySeparatedValuesRecordBoundaryAnalyzer _inner = new();
        private int _compactAnalyses;

        public int CompactAnalyses => Volatile.Read(ref _compactAnalyses);

        public SeparatedValuesBlockAnalysis Analyze(SeparatedValuesByteBlock block)
        {
            return Classify(_inner.Analyze(block));
        }

        public SeparatedValuesBlockAnalysis Analyze(
            SeparatedValuesByteBlock block,
            SeparatedValuesDialect dialect)
        {
            return Classify(_inner.Analyze(block, dialect));
        }

        public SeparatedValuesBlockAnalysis AnalyzeFraming(
            SeparatedValuesByteBlock block,
            SeparatedValuesFramingAnalysisOptions options,
            SeparatedValuesDialect dialect)
        {
            return Classify(_inner.AnalyzeFraming(block, options, dialect));
        }

        private SeparatedValuesBlockAnalysis Classify(SeparatedValuesBlockAnalysis analysis)
        {
            if (analysis.IsCompact)
                Interlocked.Increment(ref _compactAnalyses);
            return analysis;
        }
    }

    private sealed class TrackingBlockSourceFactory(
        byte[] bytes,
        Task? blockedRead = null,
        long blockedSequence = -1) : ISeparatedValuesByteBlockSourceFactory
    {
        private int _activeReads;
        private int _maximumConcurrentReads;

        public int MaximumConcurrentReads => Volatile.Read(ref _maximumConcurrentReads);

        public ISeparatedValuesByteBlockSource Open(string path, long expectedLength)
        {
            Assert.AreEqual(bytes.LongLength, expectedLength);
            return new TrackingBlockSource(this, bytes, blockedRead, blockedSequence);
        }

        private sealed class TrackingBlockSource(
            TrackingBlockSourceFactory owner,
            byte[] bytes,
            Task? blockedRead,
            long blockedSequence) : ISeparatedValuesByteBlockSource
        {
            public async ValueTask<SeparatedValuesByteBlock> ReadAsync(
                long sequence,
                long offset,
                int count,
                CancellationToken cancellationToken)
            {
                var active = Interlocked.Increment(ref owner._activeReads);
                UpdateMaximum(active);
                var buffer = ArrayPool<byte>.Shared.Rent(count);
                try
                {
                    if (sequence == blockedSequence && blockedRead is not null)
                        await blockedRead.WaitAsync(cancellationToken);
                    await Task.Delay(5, cancellationToken);
                    bytes.AsSpan((int)offset, count).CopyTo(buffer);
                    return new SeparatedValuesByteBlock(sequence, offset, buffer, count);
                }
                catch
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    throw;
                }
                finally
                {
                    Interlocked.Decrement(ref owner._activeReads);
                }
            }

            public void Dispose()
            {
            }

            private void UpdateMaximum(int active)
            {
                while (true)
                {
                    var current = Volatile.Read(ref owner._maximumConcurrentReads);
                    if (active <= current ||
                        Interlocked.CompareExchange(ref owner._maximumConcurrentReads, active, current) == current)
                        return;
                }
            }
        }
    }

    private sealed class SignalingOutputMemoryBudget(TaskCompletionSource<bool> signal)
        : ISeparatedValuesOutputMemoryBudget
    {
        public int CapacityBytes => int.MaxValue;

        public long CurrentReservedBytes => 0;

        public long HighWaterBytes => 0;

        public long OversizedReservationCount => 0;

        public long LargestOversizedRequestBytes => 0;

        public ValueTask<ISeparatedValuesOutputMemoryLease> AcquireAsync(
            long estimatedBytes,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            signal.TrySetResult(true);
            return ValueTask.FromResult<ISeparatedValuesOutputMemoryLease>(new NoopOutputMemoryLease(estimatedBytes));
        }

        private sealed class NoopOutputMemoryLease(long requestedBytes) : ISeparatedValuesOutputMemoryLease
        {
            public long RequestedBytes { get; } = requestedBytes;

            public long ReservedBytes => RequestedBytes;

            public bool IsOversized => false;

            public void Dispose()
            {
            }
        }
    }
}

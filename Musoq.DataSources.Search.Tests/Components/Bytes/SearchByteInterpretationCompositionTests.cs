#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;

using Musoq.DataSources.Search.Tests.Infrastructure;

namespace Musoq.DataSources.Search.Tests.Components.Bytes;

[TestClass]
public sealed class SearchByteInterpretationCompositionTests
{
    private const string SignatureHex = "ca ??";

    private const string NestedSignatureHex = "ca fe";

    [TestMethod]
    public void SearchBytes_TryInterpret_ShouldKeepFalseSignaturesAndInvalidLengthsAsCandidates()
    {
        var root = CreateTemporaryRoot();

        try
        {
            WriteFixture(root, "01-valid.bin", [0xCA, 0xFE, 0x02, 0xA1, 0xB2, 0x7F]);
            WriteFixture(root, "02-false-magic.bin", [0xCA, 0x00, 0x02, 0xA1, 0xB2, 0x7F]);
            WriteFixture(root, "03-invalid-length.bin", [0xCA, 0xFE, 0x09, 0xA1, 0xB2, 0x7F]);

            var result = Compile($@"
                binary BoundedRecord {{
                    Magic: byte[2] magic [0xCA, 0xFE],
                    Length: byte check Length >= 1 and Length <= 4,
                    Payload: byte[Length],
                    Trailer: byte const 0x7F
                }};
                select
                    candidate.Path,
                    candidate.ByteOffset,
                    candidate.WindowStartByteOffset,
                    candidate.WindowByteLength,
                    candidate.WindowComplete,
                    record.Length,
                    record.Trailer
                from search.bytes('{Escape(root)}', '{SignatureHex}', (Window: (BeforeBytes: 0, AfterBytes: 4))) candidate
                outer apply TryInterpret<BoundedRecord>(candidate.WindowBytes) record
                order by candidate.Path").Run();

            Assert.AreEqual(3, result.Count);
            CollectionAssert.AreEqual(
                new[] { "01-valid.bin", "02-false-magic.bin", "03-invalid-length.bin" },
                Enumerable.Range(0, result.Count).Select(index => result[index][0]).ToArray());

            Assert.AreEqual(0L, result[0][1]);
            Assert.AreEqual(0L, result[0][2]);
            Assert.AreEqual(6L, result[0][3]);
            Assert.AreEqual(true, result[0][4]);
            Assert.AreEqual((byte)2, result[0][5]);
            Assert.AreEqual((byte)0x7F, result[0][6]);

            Assert.AreEqual(0L, result[1][1]);
            Assert.AreEqual(0L, result[1][2]);
            Assert.AreEqual(6L, result[1][3]);
            Assert.AreEqual(true, result[1][4]);
            Assert.IsNull(result[1][5]);
            Assert.IsNull(result[1][6]);

            Assert.AreEqual(0L, result[2][1]);
            Assert.AreEqual(0L, result[2][2]);
            Assert.AreEqual(6L, result[2][3]);
            Assert.AreEqual(true, result[2][4]);
            Assert.IsNull(result[2][5]);
            Assert.IsNull(result[2][6]);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void SearchBytes_Interpret_ShouldParseACompleteBoundedRecord()
    {
        var root = CreateTemporaryRoot();

        try
        {
            WriteFixture(root, "record.bin", [0xCA, 0xFE, 0x02, 0xA1, 0xB2, 0x7F]);

            var result = Compile($@"
                binary BoundedRecord {{
                    Magic: byte[2] magic [0xCA, 0xFE],
                    Length: byte check Length >= 1 and Length <= 4,
                    Payload: byte[Length],
                    Trailer: byte const 0x7F
                }};
                select candidate.Path, candidate.ByteOffset, record.Length, record.Trailer
                from search.bytes('{Escape(root)}', '{SignatureHex}', (Window: (BeforeBytes: 0, AfterBytes: 4))) candidate
                cross apply Interpret<BoundedRecord>(candidate.WindowBytes) record").Run();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("record.bin", result[0][0]);
            Assert.AreEqual(0L, result[0][1]);
            Assert.AreEqual((byte)2, result[0][2]);
            Assert.AreEqual((byte)0x7F, result[0][3]);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [TestMethod]
    public void SearchBytes_PartialInterpret_ShouldExposeNestedOverreadEvidenceAlongsideCandidate()
    {
        var root = CreateTemporaryRoot();

        try
        {
            WriteFixture(root, "01-valid-nested.bin", [0xCA, 0xFE, 0x78, 0x56, 0x34, 0x12, 0x7F]);
            WriteFixture(root, "02-nested-overread.bin", [0xCA, 0xFE, 0x78, 0x56]);

            var result = Compile($@"
                binary InnerRecord {{
                    Value: int le
                }};
                binary OuterRecord {{
                    Magic: byte[2] magic [0xCA, 0xFE],
                    Payload: InnerRecord,
                    Trailer: byte const 0x7F
                }};
                select
                    candidate.Path,
                    candidate.ByteOffset,
                    candidate.WindowByteLength,
                    candidate.WindowComplete,
                    parsed.ErrorField,
                    parsed.ErrorMessage,
                    parsed.BytesConsumed
                from search.bytes('{Escape(root)}', '{NestedSignatureHex}', (Window: (BeforeBytes: 0, AfterBytes: 5))) candidate
                cross apply PartialInterpret<OuterRecord>(candidate.WindowBytes) parsed
                order by candidate.Path").Run();

            Assert.AreEqual(2, result.Count);

            Assert.AreEqual("01-valid-nested.bin", result[0][0]);
            Assert.AreEqual(0L, result[0][1]);
            Assert.AreEqual(7L, result[0][2]);
            Assert.AreEqual(true, result[0][3]);
            Assert.IsNull(result[0][4]);
            Assert.IsNull(result[0][5]);
            Assert.AreEqual(7, result[0][6]);

            Assert.AreEqual("02-nested-overread.bin", result[1][0]);
            Assert.AreEqual(0L, result[1][1]);
            Assert.AreEqual(4L, result[1][2]);
            Assert.AreEqual(false, result[1][3]);
            Assert.AreEqual("Payload.Value", result[1][4]);
            StringAssert.Contains((string)result[1][5]!, "ISE0001");
            StringAssert.Contains((string)result[1][5]!, "Payload.Value");
            Assert.AreEqual(2, result[1][6]);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static CompiledQuery Compile(string query)
    {
        return InstanceCreatorHelpers.CompileForExecution(
            query,
            Guid.NewGuid().ToString(),
            new SearchSchemaProvider(),
            EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
    }

    private static string CreateTemporaryRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"musoq-search-byte-interpretation-{Guid.NewGuid():N}");
    }

    private static void WriteFixture(string root, string name, byte[] content)
    {
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, name), content);
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal);
    }

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}

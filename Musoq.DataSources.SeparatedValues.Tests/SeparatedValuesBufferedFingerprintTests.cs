using System;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Structured;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public sealed class SeparatedValuesBufferedFingerprintTests
{
    [TestMethod]
    public void BufferedValidation_WhenContentChangesWithStableLengthAndTimestamp_RejectsDrift()
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-buffered-fingerprint-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, "Value\n1\n", new UTF8Encoding(false));

        try
        {
            var identity = StructuredFileIdentity.Capture(path, "test");
            File.WriteAllText(path, "Value\n2\n", new UTF8Encoding(false));
            File.SetLastWriteTimeUtc(path, new DateTime(identity.LastWriteTimeUtcTicks, DateTimeKind.Utc));

            using var reader = new SeparatedValuesUtf8Reader(
                path,
                (byte)',',
                0,
                4096,
                CancellationToken.None);

            var exception = Assert.ThrowsExactly<StructuredSchemaDriftException>(() =>
                reader.EnsureBufferedFingerprintMatches(identity));

            StringAssert.Contains(exception.Message, "file identity changed after planning");
        }
        finally
        {
            File.Delete(path);
        }
    }
}

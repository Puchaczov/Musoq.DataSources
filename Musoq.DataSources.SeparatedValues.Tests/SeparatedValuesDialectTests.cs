#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public sealed class SeparatedValuesDialectTests
{
    [TestMethod]
    public void ConfiguredDialect_HandlesCommentsTrimNullsAnyEndingsAndMultilineFields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-dialect-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, "# comment\r\n1 ; \"a;b\"\r\n2;NULL\r\n\r\n3;\"line\r\nnext\"\r");
        try
        {
            var dialect = SeparatedValuesDialect.FromRuntimeSettings(
                (byte)';',
                new Dictionary<string, string>
                {
                    [SeparatedValuesDialect.WhitespaceSettingName] = "trim",
                    [SeparatedValuesDialect.CommentPrefixSettingName] = "#",
                    [SeparatedValuesDialect.NullTokensSettingName] = "[\"NULL\"]",
                    [SeparatedValuesDialect.BlankRecordSettingName] = "emit",
                    [SeparatedValuesDialect.RecordEndingsSettingName] = "any"
                });

            using var reader = new SeparatedValuesUtf8Reader(path, dialect, 0, 4096, CancellationToken.None);
            var rows = new List<string?[]>();
            while (reader.TryRead(out var record))
            {
                var fields = new List<string?>();
                foreach (var field in record)
                    fields.Add(SeparatedValuesValueConverter.IsNull(field) ? null : field.Decode());
                rows.Add(fields.ToArray());
            }

            Assert.AreEqual(4, rows.Count);
            CollectionAssert.AreEqual(new[] { "1", "a;b" }, rows[0]);
            CollectionAssert.AreEqual(new[] { "2", null }, rows[1]);
            CollectionAssert.AreEqual(new string?[] { null }, rows[2]);
            CollectionAssert.AreEqual(new[] { "3", "line\r\nnext" }, rows[3]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ConfiguredDialect_BackslashEscapesAreDecodedAndComparedAsBytes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-dialect-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, "1;\"a\\\"b\"\n");
        try
        {
            var dialect = SeparatedValuesDialect.FromRuntimeSettings(
                (byte)';',
                new Dictionary<string, string>
                {
                    [SeparatedValuesDialect.EscapeSettingName] = "backslash"
                });

            using var reader = new SeparatedValuesUtf8Reader(path, dialect, 0, 4096, CancellationToken.None);
            Assert.IsTrue(reader.TryRead(out var record));
            var values = new List<string>();
            foreach (var field in record)
                values.Add(field.Decode());
            CollectionAssert.AreEqual(new[] { "1", "a\"b" }, values);
            Assert.IsFalse(reader.TryRead(out _));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ConfiguredDialect_SupportsCustomQuoteCharacters()
    {
        var path = Path.Combine(Path.GetTempPath(), $"musoq-dialect-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, "1;\'a\'\'b\'\n");
        try
        {
            var dialect = SeparatedValuesDialect.FromRuntimeSettings(
                (byte)';',
                new Dictionary<string, string>
                {
                    [SeparatedValuesDialect.QuoteSettingName] = "\'"
                });
            using var reader = new SeparatedValuesUtf8Reader(path, dialect, 0, 4096, CancellationToken.None);
            Assert.IsTrue(reader.TryRead(out var record));
            var values = new List<string>();
            foreach (var field in record)
                values.Add(field.Decode());
            CollectionAssert.AreEqual(new[] { "1", "a\'b" }, values);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

#nullable enable

using System;
using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class SeparatedValuesDecimalParserTests
{
    [DataTestMethod]
    [DataRow("0.0")]
    [DataRow("-9.9")]
    [DataRow("+12.50")]
    [DataRow(".5")]
    [DataRow("1.")]
    [DataRow("79228162514264337593543950335")]
    [DataRow("79228162514264337593543950335.0")]
    [DataRow("0.0000000000000000000000000001")]
    [DataRow("79228162514264337593543950336")]
    [DataRow("1e2")]
    [DataRow("1.2.3")]
    public void TryParse_MatchesUtf8Parser(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var expectedSuccess = Utf8Parser.TryParse(bytes, out decimal expected, out var consumed) &&
                              consumed == bytes.Length;

        var actualSuccess = SeparatedValuesDecimalParser.TryParse(bytes, out var actual);

        Assert.AreEqual(expectedSuccess, actualSuccess, text);
        if (expectedSuccess)
            Assert.AreEqual(expected, actual, text);
    }

    [TestMethod]
    public void TryParse_RandomPlainDecimals_MatchesInvariantDecimalParser()
    {
        var random = new Random(13_731);
        for (var iteration = 0; iteration < 10_000; iteration++)
        {
            var whole = random.NextInt64(0, 1_000_000_000_000);
            var fraction = random.Next(0, 1_000_000);
            var sign = random.Next(2) == 0 ? string.Empty : "-";
            var text = string.Create(
                CultureInfo.InvariantCulture,
                $"{sign}{whole}.{fraction:D6}");
            var bytes = Encoding.UTF8.GetBytes(text);

            Assert.IsTrue(SeparatedValuesDecimalParser.TryParse(bytes, out var actual), text);
            Assert.AreEqual(decimal.Parse(text, CultureInfo.InvariantCulture), actual, text);
        }
    }
}

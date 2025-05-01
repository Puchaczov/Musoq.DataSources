using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Roslyn.Components.NuGet.Version;

namespace Musoq.DataSources.Roslyn.Tests
{
    [TestClass]
    public class VersionsRangeDifferentLengthTests
    {
        [TestMethod]
        public void DifferentLengthVersions_ShouldCompareCorrectly()
        {
            // Arrange - Various versions with different lengths
            var testVersions = new[] { 
                "1.0.0", 
                "1.0.0.0", 
                "1.0.0.1", 
                "1.0.1", 
                "1.0.1.0" 
            };
            
            // Act & Assert - 1.0.0 and 1.0.0.0 are not equal
            var actual1 = ParseAndResolve("(1.0.0, 1.0.0.1)", testVersions);
            CollectionAssert.AreEqual(new[] { "1.0.0.0" }, actual1);
            
            // Act & Assert - 1.0.0.1 should be greater than 1.0.0.0
            var actual2 = ParseAndResolve("(1.0.0.0, 1.0.1)", testVersions);
            CollectionAssert.AreEqual(new[] { "1.0.0.1" }, actual2);
        }

        [TestMethod]
        public void TrailingZeros_ShouldNotAffectComparison()
        {
            // Arrange - Testing versions with trailing zeros
            var testVersions = new[] { "1.0.0", "1.0.0.0", "1.0.0.0.0" };
            
            // Act & Assert - Trailing zeros should not affect version equality
            var actual1 = ParseAndResolve("[1.0.0, 1.0.0.0]", testVersions);
            CollectionAssert.AreEqual(new[] { "1.0.0", "1.0.0.0" }, actual1);
            
            var actual2 = ParseAndResolve("[1.0.0.0, 1.0.0.0.0]", testVersions);
            CollectionAssert.AreEqual(new[] { "1.0.0.0", "1.0.0.0.0" }, actual2);
            
            var actual3 = ParseAndResolve("(1.0.0, 1.0.1)", testVersions);
            CollectionAssert.AreEqual(new[] { "1.0.0.0", "1.0.0.0.0" }, actual3);
        }

        [TestMethod]
        public void ManyPartsVersusFewerParts_ShouldCompareCorrectly()
        {
            // Arrange - Testing versions with many parts vs fewer parts
            var testVersions = new[] { "1.0.0", "1.0.0.0.0.0.0.0", "1.0.1" };
            
            // Act - Both 1.0.0 and the long version should be less than 1.0.1
            var actual = ParseAndResolve("[1.0.0, 1.0.1)", testVersions);
            
            // Assert - Version with many parts should be included in the range
            CollectionAssert.AreEqual(new[] { "1.0.0", "1.0.0.0.0.0.0.0" }, actual);
        }

        [TestMethod]
        public void ShortVersionWithLargerValues_ShouldCompareCorrectly()
        {
            // Arrange - Testing when shorter version has larger leading values
            var testVersions = new[] { "2.0.0", "1.9.9.9.9.9" };
            
            // Act & Assert - 2.0.0 should be greater than 1.9.9.9.9.9
            var actual1 = ParseAndResolve("(1.9.9.9.9.9, 3.0.0)", testVersions);
            CollectionAssert.AreEqual(new[] { "2.0.0" }, actual1);
            
            var actual2 = ParseAndResolve("[1.0.0, 2.0.0)", testVersions);
            CollectionAssert.AreEqual(new[] { "1.9.9.9.9.9" }, actual2);
        }

        [TestMethod]
        public void ZeroVsNoValue_ShouldCompareCorrectly()
        {
            // Arrange - Testing explicit zeros vs implied zeros
            var testVersions = new[] { "1.0", "1.0.0", "1" };
            
            // Act - When comparing 1.0 vs 1.0.0 vs 1
            var actual = ParseAndResolve("[1, 1.0.1)", testVersions);
            
            // Assert - Should handle implied zeros correctly, but order doesn't matter
            CollectionAssert.AreEquivalent(new[] { "1", "1.0", "1.0.0" }, actual);
        }

        [TestMethod]
        public void ExtremelyDifferentLengths_ShouldCompareCorrectly()
        {
            // Arrange - Testing versions with extremely different lengths
            var testVersions = new[] { "1", "1.0.0.0.0.0.0.0.0.0.0.0.0.1" };
            
            // Act - Compare very different length versions
            var actual = ParseAndResolve("(1, 2)", testVersions);
            
            // Assert - Extremely long version should be greater than short version
            CollectionAssert.AreEqual(new[] { "1.0.0.0.0.0.0.0.0.0.0.0.0.1" }, actual);
        }

        [TestMethod]
        public void ComplexRangeWithDifferentLengths_ShouldWorkCorrectly()
        {
            // Arrange - Various versions with different lengths
            var testVersions = new[] { 
                "1.0.0", "1.0.0.1", "1.0.1", "1.1", "1.1.0", 
                "1.1.0.0", "1.1.1", "2.0", "2.0.0.0" 
            };
            
            // Act - Test a complex range with OR operations
            var actual = ParseAndResolve("(1.0.0, 1.0.2) || [1.1, 1.1.0.0] || 2.0", testVersions);
            
            // Assert - Should correctly include all matching versions
            CollectionAssert.AreEqual(new[] { "1.0.0.1", "1.0.1", "1.1", "1.1.0", "1.1.0.0", "2.0" }, actual);
        }

        private static List<string> ParseAndResolve(string versionRange, IEnumerable<string> availableVersions)
        {
            var lexer = new VersionRangeLexer(versionRange);
            var parser = new VersionRangeParser(lexer.Tokenize());
            var range = parser.Parse();

            return range.ResolveVersions(availableVersions).ToList();
        }
    }
}


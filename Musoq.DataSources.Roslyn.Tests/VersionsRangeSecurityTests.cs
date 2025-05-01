using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Roslyn.Components.NuGet.Version;

namespace Musoq.DataSources.Roslyn.Tests
{
    [TestClass]
    public class VersionsRangeSecurityTests
    {
        private static readonly string[] TestVersions = [
            "1.0.0", "2.0.0", "3.0.0"
        ];

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void MalformedRange_MissingClosingBracket_ShouldThrowException()
        {
            // Arrange
            var malformedRange = "[1.0.0, 2.0.0";
            
            // Act - Should throw exception
            ParseAndResolve(malformedRange, TestVersions);
            
            // Assert is handled by ExpectedException attribute
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void MalformedRange_MissingComma_ShouldThrowException()
        {
            // Arrange
            var malformedRange = "[1.0.0 2.0.0]";
            
            // Act - Should throw exception
            ParseAndResolve(malformedRange, TestVersions);
            
            // Assert is handled by ExpectedException attribute
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void MalformedRange_ExtraClosingBrackets_ShouldThrowException()
        {
            // Arrange - Extra closing brackets
            var malformedRange = "[1.0.0, 2.0.0]]";
            
            // Act - Should throw exception
            ParseAndResolve(malformedRange, TestVersions);
            
            // Assert is handled by ExpectedException attribute
        }

        [TestMethod]
        public void EmptyVersionRange_ShouldHandleGracefully()
        {
            // Arrange
            var emptyRange = "";
            
            // Act & Assert - Should not throw, but might return an empty result or throw a specific exception
            Assert.ThrowsException<InvalidOperationException>(() => ParseAndResolve(emptyRange, TestVersions));
        }

        [TestMethod]
        public void WhitespaceOnlyVersionRange_ShouldHandleGracefully()
        {
            // Arrange
            var whitespaceRange = "   \t   \n   ";
            
            // Act & Assert
            Assert.ThrowsException<InvalidOperationException>(() => ParseAndResolve(whitespaceRange, TestVersions));
        }

        [TestMethod]
        public void VeryLongVersionRange_ShouldNotCauseStackOverflow()
        {
            // Arrange - Create a very long but valid version range
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 100; i++)
            {
                if (i > 0) sb.Append(" || ");
                sb.Append($"[{i}.0.0, {i+1}.0.0)");
            }
            
            // Act - This should complete without stack overflow
            var result = ParseAndResolve(sb.ToString(), TestVersions);
            
            // Assert - Should return our test versions that match the range
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void ExtremelyLongVersionNumbers_ShouldHandleGracefully()
        {
            // Arrange - Very long version numbers
            var longVersionRange = "[1.0.0, 999999999999999999.999999999999999.999999999999999]";
            
            // Act
            var result = ParseAndResolve(longVersionRange, TestVersions);
            
            // Assert - Only versions in range should be returned
            Assert.IsNotNull(result);
            // All test versions should be in the range [1.0.0, very large number]
            CollectionAssert.AreEquivalent(TestVersions, result.ToArray());
        }

        [TestMethod]
        public void MalformedVersionNumbers_ShouldHandleGracefully()
        {
            // Arrange - Invalid version formats
            var invalidRange = "[abc, def]";
            
            // Act & Assert - Should throw an exception but not crash
            Assert.ThrowsException<InvalidOperationException>(() => ParseAndResolve(invalidRange, TestVersions));
        }

        [TestMethod]
        public void DeepNestedExpressions_ShouldHandleGracefully()
        {
            // Arrange - Create a deeply nested expression through many OR operations
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 1000; i++)
            {
                if (i > 0) sb.Append(" || ");
                sb.Append($"{i}.0.0");
            }
            
            // Act - This should complete without stack overflow
            var result = ParseAndResolve(sb.ToString(), TestVersions);
            
            // Assert
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void SpecialCharactersInVersionRange_ShouldHandleGracefully()
        {
            // Arrange - Various special characters
            var specialCharsRange = "[1.0.0, 2.0.0] || @#$%^&*()";
            
            // Act & Assert
            Assert.ThrowsException<InvalidOperationException>(() => ParseAndResolve(specialCharsRange, TestVersions));
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

using System.Text;
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
            
            // Act
            ParseAndResolve(malformedRange, TestVersions);
            
            // Assert is handled by ExpectedException attribute
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void MalformedRange_MissingComma_ShouldThrowException()
        {
            // Arrange
            var malformedRange = "[1.0.0 2.0.0]";
            
            // Act
            ParseAndResolve(malformedRange, TestVersions);
            
            // Assert is handled by ExpectedException attribute
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void MalformedRange_ExtraClosingBrackets_ShouldThrowException()
        {
            // Arrange
            var malformedRange = "[1.0.0, 2.0.0]]";
            
            // Act
            ParseAndResolve(malformedRange, TestVersions);
            
            // Assert is handled by ExpectedException attribute
        }

        [TestMethod]
        public void EmptyVersionRange_ShouldHandleGracefully()
        {
            // Arrange
            var emptyRange = "";
            
            // Act & Assert
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
            // Arrange
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 100; i++)
            {
                if (i > 0) sb.Append(" || ");
                sb.Append($"[{i}.0.0, {i+1}.0.0)");
            }
            
            // Act
            var result = ParseAndResolve(sb.ToString(), TestVersions);
            
            // Assert
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void ExtremelyLongVersionNumbers_ShouldHandleGracefully()
        {
            // Arrange
            var longVersionRange = "[1.0.0, 999999999999999999.999999999999999.999999999999999]";
            
            // Act
            var result = ParseAndResolve(longVersionRange, TestVersions);
            
            // Assert
            Assert.IsNotNull(result);
            CollectionAssert.AreEquivalent(TestVersions, result.ToArray());
        }

        [TestMethod]
        public void MalformedVersionNumbers_ShouldHandleGracefully()
        {
            // Arrange
            var invalidRange = "[abc, def]";
            
            // Act & Assert
            Assert.ThrowsException<InvalidOperationException>(() => ParseAndResolve(invalidRange, TestVersions));
        }

        [TestMethod]
        public void DeepNestedExpressions_ShouldHandleGracefully()
        {
            // Arrange
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 1000; i++)
            {
                if (i > 0) sb.Append(" || ");
                sb.Append($"{i}.0.0");
            }
            
            // Act
            var result = ParseAndResolve(sb.ToString(), TestVersions);
            
            // Assert
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void SpecialCharactersInVersionRange_ShouldHandleGracefully()
        {
            // Arrange
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

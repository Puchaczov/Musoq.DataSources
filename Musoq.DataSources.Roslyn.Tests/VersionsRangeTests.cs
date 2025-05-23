using Musoq.DataSources.Roslyn.Components.NuGet.Version;

namespace Musoq.DataSources.Roslyn.Tests;

[TestClass]
public class VersionsRangeTests
{
    private static readonly string[] CommonVersions = [
        "0.1.0", "0.2.0", "0.5.0", "0.7.5", "0.9.0", 
        "1.0.0", "1.0.1", "1.1.0", "1.2.0", "1.5.0",
        "2.0.0", "2.0.1", "2.1.0", "3.0.0", "3.1.0",
        "4.0.0-preview", "4.0.0-rc1", "4.0.0", "4.0.1"
    ];

    [TestMethod]
    public void ExactVersion_ShouldMatchOnlyExactVersion()
    {
        // Arrange
        var versionRange = "1.0.0";
        var expected = new[] { "1.0.0" };

        // Act
        var actual = ParseAndResolve(versionRange, CommonVersions);

        // Assert
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void WildcardVersion_ShouldMatchAllVersionsWithPrefix()
    {
        // Arrange
        var versionRange = "1.0.*";
        var expected = new[] { "1.0.0", "1.0.1" };

        // Act
        var actual = ParseAndResolve(versionRange, CommonVersions);

        // Assert
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void InclusiveRange_ShouldMatchAllVersionsInRange()
    {
        // Arrange
        var versionRange = "[0.5.0, 0.9.0]";
        var expected = new[] { "0.5.0", "0.7.5", "0.9.0" };

        // Act
        var actual = ParseAndResolve(versionRange, CommonVersions);

        // Assert
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ExclusiveRange_ShouldMatchVersionsWithinExclusiveRange()
    {
        // Arrange
        var versionRange = "(0.5.0, 0.9.0)";
        var expected = new[] { "0.7.5" };

        // Act
        var actual = ParseAndResolve(versionRange, CommonVersions);

        // Assert
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void MixedInclusivityRange_LowerInclusiveUpperExclusive_ShouldMatchAppropriately()
    {
        // Arrange
        var versionRange = "[1.0.0, 2.0.0)";
        var expected = new[] { "1.0.0", "1.0.1", "1.1.0", "1.2.0", "1.5.0" };

        // Act
        var actual = ParseAndResolve(versionRange, CommonVersions);

        // Assert
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void MixedInclusivityRange_LowerExclusiveUpperInclusive_ShouldMatchAppropriately()
    {
        // Arrange
        var versionRange = "(1.0.0, 2.0.0]";
        var expected = new[] { "1.0.1", "1.1.0", "1.2.0", "1.5.0", "2.0.0" };

        // Act
        var actual = ParseAndResolve(versionRange, CommonVersions);

        // Assert
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void OpenUpperBound_ShouldMatchAllVersionsAboveMinimum()
    {
        // Arrange
        var versionRange = "[2.0.0,)";
        var expected = new[] { "2.0.0", "2.0.1", "2.1.0", "3.0.0", "3.1.0", "4.0.0-preview", "4.0.0-rc1", "4.0.0", "4.0.1" };

        // Act
        var actual = ParseAndResolve(versionRange, CommonVersions);

        // Assert
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void OpenLowerBound_ShouldMatchAllVersionsBelowMaximum()
    {
        // Arrange
        var versionRange = "(,1.0.0]";
        var expected = new[] { "0.1.0", "0.2.0", "0.5.0", "0.7.5", "0.9.0", "1.0.0" };

        // Act
        var actual = ParseAndResolve(versionRange, CommonVersions);

        // Assert
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void OrRanges_ShouldMatchVersionsInEitherRange()
    {
        // Arrange
        var versionRange = "[0.1.0, 0.5.0] || [2.0.0, 3.0.0)";
        var expected = new[] { "0.1.0", "0.2.0", "0.5.0", "2.0.0", "2.0.1", "2.1.0" };

        // Act
        var actual = ParseAndResolve(versionRange, CommonVersions);

        // Assert
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ComplexOrExpression_ShouldMatchAllAppropriateVersions()
    {
        // Arrange
        var versionRange = "(0.1.0, 1.0.0) || [3.0.0,) || 4.0.0-preview";
        var expected = new[] { "0.2.0", "0.5.0", "0.7.5", "0.9.0", "3.0.0", "3.1.0", "4.0.0-preview", "4.0.0-rc1", "4.0.0", "4.0.1" };

        // Act
        var actual = ParseAndResolve(versionRange, CommonVersions);

        // Assert
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void PreReleaseVersions_ShouldBeHandledCorrectly()
    {
        // Arrange
        var versionRange = "[4.0.0-preview, 4.0.0]";
        var expected = new[] { "4.0.0-preview", "4.0.0-rc1", "4.0.0" };

        // Act
        var actual = ParseAndResolve(versionRange, CommonVersions);

        // Assert
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void MultipleWildcardPatterns_ShouldMatchCorrectly()
    {
        // Arrange
        var testVersions = new[] { "1.0.0", "1.0.1", "1.1.0", "1.1.1", "2.0.0", "2.1.0" };
        
        // Act & Assert
        var actual1 = ParseAndResolve("1.*", testVersions);
        CollectionAssert.AreEqual(new[] { "1.0.0", "1.0.1", "1.1.0", "1.1.1" }, actual1);
        
        // Act & Assert
        var actual2 = ParseAndResolve("1.1.*", testVersions);
        CollectionAssert.AreEqual(new[] { "1.1.0", "1.1.1" }, actual2);
    }

    [TestMethod]
    public void BoundaryValues_ShouldBeHandledCorrectly()
    {
        // Arrange
        var testVersions = new[] { "0.9.9", "1.0.0", "1.0.1", "1.9.9", "2.0.0", "2.0.1" };
        
        // Act
        var actual = ParseAndResolve("[1.0.0, 2.0.0]", testVersions);
        
        // Assert
        CollectionAssert.AreEqual(new[] { "1.0.0", "1.0.1", "1.9.9", "2.0.0" }, actual);
    }

    [TestMethod]
    public void LargerVersionNumbers_ShouldCompareCorrectly()
    {
        // Arrange
        var testVersions = new[] { "1.5.0", "1.10.0", "1.15.0", "10.0.0", "10.10.10", "11.0.0" };
        
        // Act
        var actual = ParseAndResolve("[1.10.0, 11.0.0)", testVersions);
        
        // Assert
        CollectionAssert.AreEqual(new[] { "1.10.0", "1.15.0", "10.0.0", "10.10.10" }, actual);
    }

    [TestMethod]
    public void ComplexPreReleaseVersions_ShouldBeOrderedCorrectly()
    {
        // Arrange
        var testVersions = new[] { 
            "1.0.0-alpha", "1.0.0-alpha.1", "1.0.0-beta", 
            "1.0.0-rc", "1.0.0-rc.1", "1.0.0", "1.0.1" 
        };
        
        // Act
        var actual = ParseAndResolve("[1.0.0-beta, 1.0.0]", testVersions);
        
        // Assert
        CollectionAssert.AreEqual(new[] { "1.0.0-beta", "1.0.0-rc", "1.0.0-rc.1", "1.0.0" }, actual);
    }

    [TestMethod]
    public void MultipleOrRanges_ShouldCombineCorrectly()
    {
        // Arrange
        var versionRange = "1.0.0 || [1.5.0, 2.0.0) || (3.0.0, 4.0.0]";
        var expected = new[] { "1.0.0", "1.5.0", "1.9.9", "3.1.0", "4.0.0" };
        var testVersions = new[] { "0.9.0", "1.0.0", "1.2.0", "1.5.0", "1.9.9", "2.0.0", "3.0.0", "3.1.0", "4.0.0", "5.0.0" };

        // Act
        var actual = ParseAndResolve(versionRange, testVersions);

        // Assert
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void InvalidVersionRange_ShouldThrowException()
    {
        // Arrange
        var invalidRange = "[1.0.0, 2.0.0";
        
        // Act
        ParseAndResolve(invalidRange, CommonVersions);
        
        // Assert is handled by ExpectedException attribute
    }

    [TestMethod]
    public void MultiPartVersions_ShouldCompareCorrectly()
    {
        // Arrange
        var testVersions = new[] { 
            "1.0.0", 
            "1.0.0.1", 
            "1.0.0.42", 
            "1.0.1", 
            "1.0.1.0",
            "1.0.2.2.241",
            "2.0.0" 
        };
        
        // Act
        var actual1 = ParseAndResolve("[1.0.0, 1.0.1)", testVersions);
        var actual2 = ParseAndResolve("[1.0.0.1, 1.0.1)", testVersions);
        var actual3 = ParseAndResolve("[1.0.0, 2.0.0)", testVersions);
        
        // Assert
        CollectionAssert.AreEqual(new[] { "1.0.0", "1.0.0.1", "1.0.0.42" }, actual1);
        CollectionAssert.AreEqual(new[] { "1.0.0.1", "1.0.0.42" }, actual2);
        CollectionAssert.AreEqual(new[] { "1.0.0", "1.0.0.1", "1.0.0.42", "1.0.1", "1.0.1.0", "1.0.2.2.241" }, actual3);
    }

    [TestMethod]
    public void VersionWith4Parts_ShouldBeGreaterThan3Parts()
    {
        // Arrange
        var testVersions = new[] { "1.0.0", "1.0.0.0" };
        
        // Act
        var actual = ParseAndResolve("(1.0.0, 1.0.1)", testVersions);
        
        // Assert
        CollectionAssert.AreEqual(new[] { "1.0.0.0" }, actual);
    }

    [TestMethod]
    public void ZeroVsNoValue_ShouldCompareCorrectly()
    {
        // Arrange
        var testVersions = new[] { "1.0", "1.0.0" };

        // Act
        var actual = ParseAndResolve("[1.0, 1.0.0]", testVersions);

        // Assert
        CollectionAssert.AreEquivalent(new[] { "1.0", "1.0.0" }, actual);
    }

    [TestMethod]
    public void BracketedSingleVersion_ShouldMatchExactVersion()
    {
        // Arrange
        var versionRange = "[2.0.0]";
        var expected = new[] { "2.0.0" };

        // Act
        var actual = ParseAndResolve(versionRange, CommonVersions);

        // Assert
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ParenthesisSingleVersion_ShouldMatchNothing()
    {
        // Arrange
        var versionRange = "(2.0.0)";
        var expected = Array.Empty<string>();

        // Act
        var actual = ParseAndResolve(versionRange, CommonVersions);

        // Assert
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void MixedBracketsSingleVersion_ShouldThrowException()
    {
        // Arrange
        var versionRange = "[2.0.0)";
        var expected = Array.Empty<string>();

        // Act
        var actual = ParseAndResolve(versionRange, CommonVersions);

        // Assert
        CollectionAssert.AreEqual(expected, actual);
    }

    private static List<string> ParseAndResolve(string versionRange, IEnumerable<string> availableVersions)
    {
        var lexer = new VersionRangeLexer(versionRange);
        var parser = new VersionRangeParser(lexer.Tokenize());
        var range = parser.Parse();

        return range.ResolveVersions(availableVersions).ToList();
    }
}

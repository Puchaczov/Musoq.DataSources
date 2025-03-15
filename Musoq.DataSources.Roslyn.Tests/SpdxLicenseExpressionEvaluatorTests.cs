using Musoq.DataSources.Roslyn.Components.NuGet.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Roslyn.Components.NuGet;

namespace Musoq.DataSources.Roslyn.Tests
{
    [TestClass]
    public class SpdxLicenseExpressionEvaluatorTests
    {
        [TestMethod]
        public async Task EmptyOrNullInput_ReturnsEmptyList()
        {
            string nullExpression = null;
            var emptyExpression = "";
            var whitespaceExpression = "   ";

            var nullResult = await SpdxLicenseExpressionEvaluator.GetLicenseIdentifiersAsync(nullExpression);
            var emptyResult = await SpdxLicenseExpressionEvaluator.GetLicenseIdentifiersAsync(emptyExpression);
            var whitespaceResult = await SpdxLicenseExpressionEvaluator.GetLicenseIdentifiersAsync(whitespaceExpression);

            CollectionAssert.AreEqual(new List<string>(), nullResult);
            CollectionAssert.AreEqual(new List<string>(), emptyResult);
            CollectionAssert.AreEqual(new List<string>(), whitespaceResult);
        }

        [TestMethod]
        public async Task SingleLicense_ReturnsCorrectIdentifier()
        {
            var expression = "MIT";

            var result = await SpdxLicenseExpressionEvaluator.GetLicenseIdentifiersAsync(expression);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("MIT", result[0]);
        }

        [TestMethod]
        [DataRow("MIT AND Apache-2.0", new[] { "MIT", "Apache-2.0" })]
        [DataRow("MIT OR Apache-2.0", new[] { "MIT", "Apache-2.0" })]
        public async Task SimpleOperators_ReturnCorrectIdentifiers(string expression, string[] expected)
        {
            var result = await SpdxLicenseExpressionEvaluator.GetLicenseIdentifiersAsync(expression);

            Assert.AreEqual(expected.Length, result.Count);
            foreach (var id in expected)
            {
                CollectionAssert.Contains(result, id);
            }
        }

        [TestMethod]
        [DataRow("(MIT OR Apache-2.0) AND GPL-3.0", new[] { "MIT", "Apache-2.0", "GPL-3.0" })]
        [DataRow("MIT OR (Apache-2.0 AND GPL-3.0)", new[] { "MIT", "Apache-2.0", "GPL-3.0" })]
        [DataRow("((MIT OR BSD-3-Clause) AND GPL-2.0) OR LGPL-2.1", new[] { "MIT", "BSD-3-Clause", "GPL-2.0", "LGPL-2.1" })]
        public async Task NestedExpressions_ReturnCorrectIdentifiers(string expression, string[] expected)
        {
            var result = await SpdxLicenseExpressionEvaluator.GetLicenseIdentifiersAsync(expression);

            Assert.AreEqual(expected.Length, result.Count);
            foreach (var id in expected)
            {
                CollectionAssert.Contains(result, id);
            }
        }

        [TestMethod]
        [DataRow("GPL-3.0-only WITH Autoconf-exception-3.0", new[] { "GPL-3.0-only", "Autoconf-exception-3.0" })]
        [DataRow("GPL-2.0+ WITH Bison-exception-2.2", new[] { "GPL-2.0+", "Bison-exception-2.2" })]
        public async Task WithExceptions_ReturnCorrectIdentifiers(string expression, string[] expected)
        {
            var result = await SpdxLicenseExpressionEvaluator.GetLicenseIdentifiersAsync(expression);

            Assert.AreEqual(expected.Length, result.Count);
            foreach (var id in expected)
            {
                CollectionAssert.Contains(result, id);
            }
        }

        [TestMethod]
        [DataRow("(GPL-3.0-only WITH Autoconf-exception-3.0) OR MIT", new[] { "GPL-3.0-only", "Autoconf-exception-3.0", "MIT" })]
        [DataRow("MIT AND (Apache-2.0 OR (BSD-3-Clause WITH GCC-exception-3.1))", new[] { "MIT", "Apache-2.0", "BSD-3-Clause", "GCC-exception-3.1" })]
        [DataRow("(LGPL-2.1+ WITH Unix-exception-2.0) AND (GPL-2.0 OR MIT)", new[] { "LGPL-2.1+", "Unix-exception-2.0", "GPL-2.0", "MIT" })]
        public async Task ComplexExpressions_ReturnCorrectIdentifiers(string expression, string[] expected)
        {
            var result = await SpdxLicenseExpressionEvaluator.GetLicenseIdentifiersAsync(expression);
            
            Assert.AreEqual(expected.Length, result.Count);
            foreach (var id in expected)
            {
                CollectionAssert.Contains(result, id);
            }
        }

        [TestMethod]
        [DataRow("  MIT  AND  Apache-2.0  ", new[] { "MIT", "Apache-2.0" })]
        [DataRow("MIT   OR\tApache-2.0", new[] { "MIT", "Apache-2.0" })]
        [DataRow("( MIT OR Apache-2.0 ) AND GPL-3.0", new[] { "MIT", "Apache-2.0", "GPL-3.0" })]
        public async Task WhitespaceVariations_ReturnCorrectIdentifiers(string expression, string[] expected)
        {
            var result = await SpdxLicenseExpressionEvaluator.GetLicenseIdentifiersAsync(expression);

            Assert.AreEqual(expected.Length, result.Count);
            foreach (var id in expected)
            {
                CollectionAssert.Contains(result, id);
            }
        }

        [TestMethod]
        [DataRow("mit AND apache-2.0", new[] { "mit", "apache-2.0" })]
        [DataRow("MIT and APACHE-2.0", new[] { "MIT", "APACHE-2.0" })]
        [DataRow("MIT Or Apache-2.0", new[] { "MIT", "Apache-2.0" })]
        [DataRow("GPL-3.0-only with Autoconf-exception-3.0", new[] { "GPL-3.0-only", "Autoconf-exception-3.0" })]
        public async Task CaseInsensitiveOperators_ReturnCorrectIdentifiers(string expression, string[] expected)
        {
            var result = await SpdxLicenseExpressionEvaluator.GetLicenseIdentifiersAsync(expression);

            Assert.AreEqual(expected.Length, result.Count);
            foreach (var id in expected)
            {
                CollectionAssert.Contains(result, id);
            }
        }

        [TestMethod]
        [DataRow("MIT AND", new[] { "MIT" })]
        [DataRow("AND Apache-2.0", new[] { "Apache-2.0" })]
        [DataRow("MIT WITH", new[] { "MIT" })]
        [DataRow("(MIT", new[] { "MIT" })]
        [DataRow("MIT)", new[] { "MIT" })]
        [DataRow("()", new string[0])]
        public async Task MalformedExpressions_ReturnAvailableIdentifiers(string expression, string[] expected)
        {
            var result = await SpdxLicenseExpressionEvaluator.GetLicenseIdentifiersAsync(expression);

            Assert.AreEqual(expected.Length, result.Count);
            foreach (var id in expected)
            {
                if (expected.Length > 0)
                {
                    CollectionAssert.Contains(result, id);
                }
            }
        }

        [TestMethod]
        public async Task DuplicateLicenses_ReturnUniqueIdentifiers()
        {
            var expression = "MIT AND MIT OR MIT WITH MIT-exception";

            var result = await SpdxLicenseExpressionEvaluator.GetLicenseIdentifiersAsync(expression);

            Assert.AreEqual(2, result.Count);
            CollectionAssert.Contains(result, "MIT");
            CollectionAssert.Contains(result, "MIT-exception");
        }

        [TestMethod]
        [DataRow("LicenseRef-123.4-ABCD_xyz", new[] { "LicenseRef-123.4-ABCD_xyz" })]
        [DataRow("CDDL-1.1+", new[] { "CDDL-1.1+" })]
        [DataRow("License.With.Dots-1.0", new[] { "License.With.Dots-1.0" })]
        public async Task SpecialCharactersInLicenseIds_ReturnCorrectIdentifiers(string expression, string[] expected)
        {
            var result = await SpdxLicenseExpressionEvaluator.GetLicenseIdentifiersAsync(expression);

            Assert.AreEqual(expected.Length, result.Count);
            foreach (var id in expected)
            {
                CollectionAssert.Contains(result, id);
            }
        }

        [TestMethod]
        [DataRow("(GPL-2.0 WITH Exception-1) AND (LGPL-2.1 WITH Exception-2)", 
                 new[] { "GPL-2.0", "Exception-1", "LGPL-2.1", "Exception-2" })]
        [DataRow("(MIT WITH MIT-exception) OR (Apache-2.0 WITH Apache-exception) OR (BSD WITH BSD-exception)",
                 new[] { "MIT", "MIT-exception", "Apache-2.0", "Apache-exception", "BSD", "BSD-exception" })]
        public async Task MultipleExceptions_ReturnAllIdentifiers(string expression, string[] expected)
        {
            var result = await SpdxLicenseExpressionEvaluator.GetLicenseIdentifiersAsync(expression);

            Assert.AreEqual(expected.Length, result.Count);
            foreach (var id in expected)
            {
                CollectionAssert.Contains(result, id);
            }
        }

        [TestMethod]
        [DataRow("(((((((((MIT)))))))))) OR Apache-2.0", new[] { "MIT", "Apache-2.0" })]
        [DataRow("MIT OR (((((Apache-2.0 AND ((((GPL-3.0))))) AND (BSD))))))", 
                 new[] { "MIT", "Apache-2.0", "GPL-3.0", "BSD" })]
        public async Task DeeplyNestedExpressions_ReturnCorrectIdentifiers(string expression, string[] expected)
        {
            var result = await SpdxLicenseExpressionEvaluator.GetLicenseIdentifiersAsync(expression);

            Assert.AreEqual(expected.Length, result.Count);
            foreach (var id in expected)
            {
                CollectionAssert.Contains(result, id);
            }
        }

        [TestMethod]
        [DataRow("MIT AND OR Apache-2.0", new[] { "MIT", "Apache-2.0" })]
        [DataRow("MIT OR AND GPL-3.0", new[] { "MIT", "GPL-3.0" })]
        [DataRow("MIT WITH AND GPL-3.0", new[] { "MIT", "GPL-3.0" })]
        [DataRow("OR MIT AND Apache-2.0", new[] { "MIT", "Apache-2.0" })]
        public async Task InvalidOperatorSequences_ReturnAvailableLicenses(string expression, string[] expected)
        {
            var result = await SpdxLicenseExpressionEvaluator.GetLicenseIdentifiersAsync(expression);

            foreach (var id in expected)
            {
                CollectionAssert.Contains(result, id);
            }
        }
    }
}
using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Schema;
using Musoq.Schema.DataSources;

namespace Musoq.DataSources.SeparatedValues.Tests;

[TestClass]
public class SeparatedValuesSchemaHeaderTests
{
    [TestMethod]
    public void Columns_WhenHeaderContainsQuotedSeparator_UsesCsvParser()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(tempFile, "\"First,Name\",Age\r\nAlice,31\r\n", Encoding.UTF8);

        try
        {
            var table = new SeparatedValuesTable(tempFile, ",", true, 0)
            {
                InferredColumns = Array.Empty<ISchemaColumn>()
            };

            var columns = table.Columns.ToArray();

            Assert.AreEqual(2, columns.Length);
            Assert.AreEqual("FirstName", columns[0].ColumnName);
            Assert.AreEqual("Age", columns[1].ColumnName);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}

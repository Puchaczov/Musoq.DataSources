using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.Tests.Common;

namespace Musoq.DataSources.Os.Tests;

[TestClass]
public sealed class NativeVariadicQueryTests
{
    [TestMethod]
    public void Combine_WhenArityExceedsFixedOverloads_ShouldConsumeTypedSpanResult()
    {
        const string sql = "select Combine('root', 'a', 'b', 'c', 'd', 'e', 'f') as Combined from os.currentculture()";
        using var query = InstanceCreatorHelpers.CompileForExecution(sql, Guid.NewGuid().ToString(),
            new OsSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        using var table = query.Run();
        var columns = table.Columns.ToArray();
        Assert.AreEqual(1, columns.Length);
        Assert.AreEqual("Combined", columns[0].ColumnName);
        Assert.AreEqual(typeof(string), columns[0].ColumnType);
        var rows = table.ToArray();
        Assert.AreEqual(1, rows.Length);
        Assert.AreEqual(Path.Combine("root", "a", "b", "c", "d", "e", "f"), rows[0].GetValue<string>(0));
    }
}

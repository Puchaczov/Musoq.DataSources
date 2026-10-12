using System.Linq;
using Musoq.DataSources.Tests.Common;

namespace Musoq.DataSources.Roslyn.Tests;

public partial class RoslynToSqlTests
{
    [TestMethod]
    public void NativeVariadicClasses_WhenMultipleNamesRequested_ShouldConsumeExactOrderedMatches()
    {
        var sql = $"select c.Name as Name from csharp.solution('{Solution1SolutionPath.Escape()}') s " +
            "cross apply s.GetClassesByNames('Class1', 'CyclomaticComplexityClass1', 'NoSuchType') c order by c.Name";
        using var query = CompileQuery(sql);
        using var table = query.Run();
        var columns = table.Columns.ToArray();
        Assert.AreEqual(1, columns.Length);
        Assert.AreEqual("Name", columns[0].ColumnName);
        Assert.AreEqual(typeof(string), columns[0].ColumnType);
        var rows = table.ToArray();
        Assert.AreEqual(2, rows.Length);
        CollectionAssert.AreEqual(new[] { "Class1", "CyclomaticComplexityClass1" },
            rows.Select(static row => row.GetValue<string>(0)).ToArray());
    }

    [TestMethod]
    public void NativeVariadicClasses_WhenNamesRepeat_ShouldPreserveMembershipAndPaging()
    {
        var sql = $"select c.Name as Name from csharp.solution('{Solution1SolutionPath.Escape()}') s " +
            "cross apply s.GetClassesByNames('Class1', 'Class1', 'CyclomaticComplexityClass1') c order by c.Name take 1";
        using var query = CompileQuery(sql);
        using var table = query.Run();
        var rows = table.ToArray();
        Assert.AreEqual(1, rows.Length);
        Assert.AreEqual("Class1", rows[0].GetValue<string>(0));
    }
}

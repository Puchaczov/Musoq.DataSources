using Musoq.DataSources.Tests.Common;

namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class BlameRevisionTests
{
    [TestMethod]
    public void ModernBlameSupportsHistoricalContentBinaryFilesAndUnicodePaths()
    {
        using var fixture = ModernWorktreeTests.Create(out var linked);
        var path = linked.Replace("\\", "\\\\");
        var rows = WorktreeCompatibilityTests.Query($"select l.Content from git.blame('{path}', 'tracked.txt') h cross apply h.Lines l order by l.LineNumber");
        CollectionAssert.AreEqual(new[] { "inserted", "first", "second changed", "third" }, rows.Select(r => (string)r.ReadValue(0)).ToArray());
        var historical = WorktreeCompatibilityTests.Query($"select l.Content from git.blame('{path}', 'tracked.txt', 'HEAD~1') h cross apply h.Lines l order by l.LineNumber");
        CollectionAssert.AreEqual(new[] { "first", "second", "third" }, historical.Select(r => (string)r.ReadValue(0)).ToArray());
        File.WriteAllBytes(Path.Combine(linked, "binary.bin"), [1, 0, 2, 0]);
        File.WriteAllText(Path.Combine(linked, "żółw.txt"), "unicode\n");
        OfflineGitFixture.RunGit(linked, "add", "binary.bin", "żółw.txt");
        OfflineGitFixture.RunGit(linked, "commit", "-qm", "binary and unicode");
        Assert.AreEqual(0, WorktreeCompatibilityTests.Query($"select * from git.blame('{path}', 'binary.bin')").Count);
        Assert.AreEqual(1, WorktreeCompatibilityTests.Query($"select * from git.blame('{path}', 'żółw.txt')").Count);
        Assert.AreEqual("żółw\n.txt", GitRepositoryContext.DecodeGitPath("\"\\305\\274\\303\\263\\305\\202w\\n.txt\""));
    }

    [TestMethod]
    public void LinesUseRequestedRevisionAfterInsertedAndModifiedLines()
    {
        using var fixture = OfflineGitFixture.Create();
        var file = Path.Combine(fixture.SeedPath, "lines.txt");
        File.WriteAllText(file, "first\nsecond\nthird\n");
        OfflineGitFixture.RunGit(fixture.SeedPath, "add", "lines.txt");
        OfflineGitFixture.RunGit(fixture.SeedPath, "commit", "-qm", "base lines");
        File.WriteAllText(file, "inserted\nfirst\nsecond changed\nthird\n");
        OfflineGitFixture.RunGit(fixture.SeedPath, "add", "lines.txt");
        OfflineGitFixture.RunGit(fixture.SeedPath, "commit", "-qm", "insert and modify");
        var path = fixture.SeedPath.Replace("\\", "\\\\");
        var rows = WorktreeCompatibilityTests.Query($"select l.Content from git.blame('{path}', 'lines.txt') h cross apply h.Lines l order by l.LineNumber");
        CollectionAssert.AreEqual(new[] { "inserted", "first", "second changed", "third" }, rows.Select(r => (string)r.ReadValue(0)).ToArray());
    }
}

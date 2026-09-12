namespace Musoq.DataSources.Git.Tests;

[TestClass]
public sealed class GitReferenceBackendOptionsTests
{
    [TestMethod]
    public void DefaultsToCliFirstAutoBackend()
    {
        var options = GitReferenceBackendOptions.From(new Dictionary<string, string>());

        Assert.AreEqual(GitHistoryBackend.Auto, options.Backend);
        Assert.AreEqual("git", options.Executable);
    }

    [TestMethod]
    [DataRow("auto", "Auto")]
    [DataRow("git-cli", "GitCli")]
    [DataRow("libgit2", "LibGit2")]
    public void ParsesBackend(string value, string expected)
    {
        var options = GitReferenceBackendOptions.From(new Dictionary<string, string>
        {
            [GitReferenceBackendOptions.BackendSettingName] = value,
            [GitReferenceBackendOptions.ExecutableSettingName] = "custom-git"
        });

        Assert.AreEqual(expected, options.Backend.ToString());
        Assert.AreEqual("custom-git", options.Executable);
    }

    [TestMethod]
    public void RejectsUnknownBackend()
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(() =>
            GitReferenceBackendOptions.From(new Dictionary<string, string>
            {
                [GitReferenceBackendOptions.BackendSettingName] = "network"
            }));

        StringAssert.Contains(exception.Message, GitReferenceBackendOptions.BackendSettingName);
    }
}

using System.Collections.Generic;

namespace Musoq.DataSources.Tests.Common;

public static class EnvironmentVariablesHelpers
{
    public static IReadOnlyDictionary<uint, IReadOnlyDictionary<string, string>> CreateMockedEnvironmentVariables()
    {
        return CreateMockedEnvironmentVariables(new Dictionary<string, string>
        {
            { "OPENAI_API_KEY", "OPENAI_API_KEY" },
            { "OLLAMA_BASE_URL", "http://localhost:11434" },
            { "GITHUB_TOKEN", "test_token" },
            { "JIRA_URL", "https://test.atlassian.net" },
            { "JIRA_USERNAME", "test@example.com" },
            { "JIRA_API_TOKEN", "test_token" },
            { "MUSOQ_SERVER_HTTP_ENDPOINT", "https://localhost/internal/this-doesnt-exists" }
        });
    }

    public static IReadOnlyDictionary<uint, IReadOnlyDictionary<string, string>> CreateMockedEnvironmentVariables(
        IReadOnlyDictionary<string, string> variables)
    {
        var data = new Dictionary<uint, IReadOnlyDictionary<string, string>>();

        for (uint i = 0; i <= 100; i++) data[i] = variables;

        return data;
    }
}

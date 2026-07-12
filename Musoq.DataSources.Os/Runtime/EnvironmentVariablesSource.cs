using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Musoq.Schema.Optimization;

namespace Musoq.DataSources.Os.Runtime;

internal sealed class EnvironmentVariablesSource(SourceExecutionContext executionContext)
    : RuntimeDiscoverySourceBase<EnvironmentVariableEntity>(executionContext, "environmentvariables")
{
    protected override IEnumerable<EnvironmentVariableEntity> GetRows()
    {
        var rows = new List<EnvironmentVariableEntity>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in new[]
                 {
                     EnvironmentVariableTarget.Process,
                     EnvironmentVariableTarget.User,
                     EnvironmentVariableTarget.Machine
                 })
        {
            foreach (var name in GetNames(target))
            {
                var key = $"{target}:{name}";
                if (seen.Add(key))
                    rows.Add(new EnvironmentVariableEntity(name, target.ToString()));
            }
        }

        return rows.OrderBy(static row => row.Target).ThenBy(static row => row.Name, StringComparer.Ordinal);
    }

    private static IEnumerable<string> GetNames(EnvironmentVariableTarget target)
    {
        IDictionary variables;

        try
        {
            variables = Environment.GetEnvironmentVariables(target);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or PlatformNotSupportedException)
        {
            yield break;
        }

        foreach (var key in variables.Keys)
        {
            if (key is string name)
                yield return name;
        }
    }
}

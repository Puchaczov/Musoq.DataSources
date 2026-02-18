using System.Collections.Generic;
using Moq;

namespace Musoq.DataSources.Tests.Common;

public static class EnvironmentVariablesHelpers
{
    public static IReadOnlyDictionary<uint, IReadOnlyDictionary<string, string>> CreateMockedEnvironmentVariables()
    {
        var environmentVariablesMock = new Mock<IReadOnlyDictionary<uint, IReadOnlyDictionary<string, string>>>();
        environmentVariablesMock.Setup(f => f[It.IsAny<uint>()]).Returns(new Dictionary<string, string>());

        return environmentVariablesMock.Object;
    }

    public static IReadOnlyDictionary<uint, IReadOnlyDictionary<string, string>> CreateMockedEnvironmentVariables(
        IReadOnlyDictionary<string, string> variables)
    {
        var environmentVariablesMock = new Mock<IReadOnlyDictionary<uint, IReadOnlyDictionary<string, string>>>();
        var data = new Dictionary<uint, IReadOnlyDictionary<string, string>>();

        for (uint i = 0; i <= 100; i++) data[i] = variables;

        environmentVariablesMock
            .Setup(x => x.GetEnumerator())
            .Returns(() => data.GetEnumerator());

        environmentVariablesMock
            .Setup(x => x.Keys)
            .Returns(data.Keys);

        environmentVariablesMock
            .Setup(x => x[It.IsAny<uint>()])
            .Returns((uint index) => data[index]);

        environmentVariablesMock
            .Setup(x => x.TryGetValue(It.IsAny<uint>(), out It.Ref<IReadOnlyDictionary<string, string>>.IsAny))
            .Returns((uint key, out IReadOnlyDictionary<string, string> val) => data.TryGetValue(key, out val));

        return environmentVariablesMock.Object;
    }
}
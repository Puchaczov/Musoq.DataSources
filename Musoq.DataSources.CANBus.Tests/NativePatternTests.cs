using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.DataSources.CANBus.Messages;
using Musoq.DataSources.CANBus.Signals;
using Musoq.DataSources.Tests.Common;
using Musoq.Schema;
namespace Musoq.DataSources.CANBus.Tests;
[TestClass]
public sealed class NativePatternTests
{
    [TestMethod]
    public void AllDbcStringColumns_MatchCore()
    {
        var schema = new CANBusSchema();
        var path = "./Data/1/1.dbc";
        var args = SourceArguments.Create(path, SourceArguments.Empty);
        PatternContractVerifier.For<MessageEntity>().Verify(schema, "messages", args, [path]);
        PatternContractVerifier.For<SignalEntity>().Verify(schema, "signals", args, [path]);
    }
}

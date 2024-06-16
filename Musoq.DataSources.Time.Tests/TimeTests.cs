using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musoq.Converter;
using Musoq.DataSources.Tests.Common;
using Musoq.Evaluator;
using Musoq.Plugins;
using Musoq.Schema;
using Environment = Musoq.Plugins.Environment;

namespace Musoq.DataSources.Time.Tests
{
    [TestClass]
    public class TimeTests
    {
        [TestMethod]
        public void EnumerateAllDaysInMonthTest()
        {
            var query = "select Day from #time.interval('01.04.2018 00:00:00', '30.04.2018 00:00:00', 'days')";

            var vm = CreateAndRunVirtualMachine(query);
            var table = vm.Run();

            Assert.AreEqual(30, table.Count);

            for (var i = 1; i <= 30; i++) Assert.AreEqual(i, table[i - 1][0]);
        }

        [TestMethod]
        public void TimeSource_CancelledLoadTest()
        {
            using var tokenSource = new CancellationTokenSource();
            tokenSource.Cancel();
            var now = DateTimeOffset.Now;
            var nextHour = now.AddHours(1);
            var source = new TimeSource(now, nextHour, "minutes", 
                new RuntimeContext(
                    tokenSource.Token, 
                    Array.Empty<ISchemaColumn>(), 
                    new Dictionary<string, string>(), 
                    (null, null, null)));

            var fired = source.Rows.Count();

            Assert.AreEqual(0, fired);
        }

        [TestMethod]
        public void TimeSource_FullLoadTest()
        {
            var now = DateTimeOffset.Parse("01/01/2000");
            var nextHour = now.AddHours(1);
            var source = new TimeSource(now, nextHour, "minutes", 
                new RuntimeContext(
                    CancellationToken.None, 
                    Array.Empty<ISchemaColumn>(), 
                    new Dictionary<string, string>(),
                    (null, null, null)));

            var fired = source.Rows.Count();

            Assert.AreEqual(61, fired);
        }

        private CompiledQuery CreateAndRunVirtualMachine(string script)
        {
            return InstanceCreator.CompileForExecution(script, Guid.NewGuid().ToString(), new TimeSchemaProvider(), EnvironmentVariablesHelpers.CreateMockedEnvironmentVariables());
        }

        static TimeTests()
        {
            new Environment().SetValue(Constants.NetStandardDllEnvironmentVariableName, EnvironmentUtils.GetOrCreateEnvironmentVariable());

            Culture.ApplyWithDefaultCulture();
        }
    }
}
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
using Rebus.Tests.Contracts.Utilities;
// ReSharper disable ArgumentsStyleLiteral
// ReSharper disable AccessToDisposedClosure

namespace Rebus.SqlServer.Tests.Transport;

[TestFixture]
public class TestSqlServerTransportCleanup : TestSqlServerTransportCleanupBase
{
}

[TestFixture]
public class TestSingleMessageTableSqlServerTransportCleanup : TestSqlServerTransportCleanupBase
{
    protected override bool UseSingleMessageTable => true;
}

public abstract class TestSqlServerTransportCleanupBase : FixtureBase
{
    BuiltinHandlerActivator _activator;
    ListLoggerFactory _loggerFactory;
    IBusStarter _starter;

    protected virtual bool UseSingleMessageTable => false;

    protected override void SetUp()
    {
        SqlTestHelper.DropAllTables();

        var queueName = TestConfig.GetName("connection_timeout");

        _activator = new BuiltinHandlerActivator();

        Using(_activator);

        _loggerFactory = new ListLoggerFactory(outputToConsole: true);

        var options = new SqlServerTransportOptions(SqlTestHelper.ConnectionString);

        if (UseSingleMessageTable)
        {
            options.UseSingleMessageTable("Messages");
        }

        _starter = Configure.With(_activator)
            .Logging(l => l.Use(_loggerFactory))
            .Transport(t => t.UseSqlServer(options, queueName))
            .Create();
    }

    [Test]
    public async Task DoesNotBarfInTheBackground()
    {
        using var doneHandlingMessage = new ManualResetEvent(false);

        _activator.Handle<string>(async str =>
        {
            for (var count = 0; count < 5; count++)
            {
                Console.WriteLine("waiting...");
                await Task.Delay(TimeSpan.FromSeconds(20));
            }

            Console.WriteLine("done waiting!");

            doneHandlingMessage.Set();
        });

        var bus = _starter.Start();
        
        await bus.SendLocal("hej med dig min ven!");

        doneHandlingMessage.WaitOrDie(TimeSpan.FromMinutes(2));

        var logLinesAboveInformation = _loggerFactory
            .Where(l => l.Level >= LogLevel.Warn)
            .ToList();

        Assert.That(!logLinesAboveInformation.Any(), "Expected no warnings - got this: {0}", string.Join(Environment.NewLine, logLinesAboveInformation));
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using Rebus.SqlServer.Transport;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
using Rebus.Tests.Contracts.Utilities;
// ReSharper disable AccessToDisposedClosure
#pragma warning disable 1998

namespace Rebus.SqlServer.Tests.Transport;

[TestFixture]
public class TestMessagePriority : TestMessagePriorityBase
{
}

[TestFixture]
public class TestSingleTableMessagePriority : TestMessagePriorityBase
{
    protected override SqlServerTransportOptions CreateSqlServerTransportOptions(string connectionString)
    {
        return base.CreateSqlServerTransportOptions(connectionString).UseSingleMessageTable("Messages");
    }

    protected override SqlServerLeaseTransportOptions CreateSqlServerLeaseTransportOptions(string connectionString)
    {
        return base.CreateSqlServerLeaseTransportOptions(connectionString).UseSingleMessageTable("Messages");
    }
}

public abstract class TestMessagePriorityBase : FixtureBase
{
    protected override void SetUp() => SqlTestHelper.DropAllTables();
    
    protected virtual SqlServerTransportOptions CreateSqlServerTransportOptions(string connectionString)
    {
        return new SqlServerTransportOptions(connectionString);
    }
    
    protected virtual SqlServerLeaseTransportOptions CreateSqlServerLeaseTransportOptions(string connectionString)
    {
        return new SqlServerLeaseTransportOptions(connectionString);
    }

    [Test]
    public async Task ReceivedMessagesByPriority_HigherIsMoreImportant_Normal() => await RunTest("normal", 20);

    [Test]
    public async Task ReceivedMessagesByPriority_HigherIsMoreImportant_LeaseBased() => await RunTest("lease-based", 20);

    private async Task RunTest(string type, int messageCount)
    {
        using var counter = new SharedCounter(messageCount);
        var receivedMessagePriorities = new List<int>();
        var server = new BuiltinHandlerActivator();

        server.Handle<string>(async str =>
        {
            Console.WriteLine($"Received message: {str}");
            var parts = str.Split(' ');
            var priority = int.Parse(parts[1]);
            receivedMessagePriorities.Add(priority);
            counter.Decrement();
        });

        var sqlServerTransportOptions = CreateSqlServerTransportOptions(SqlTestHelper.ConnectionString);
        var sqlServerLeaseTransportOptions = CreateSqlServerLeaseTransportOptions(SqlTestHelper.ConnectionString);
        
        var serverBus = Configure.With(Using(server))
            .Transport(t =>
            {
                if (type == "normal")
                {
                    t.UseSqlServer(sqlServerTransportOptions, "server");
                }
                else
                {
                    t.UseSqlServerInLeaseMode(sqlServerLeaseTransportOptions, "server");
                }
            })
            .Options(o =>
            {
                o.SetNumberOfWorkers(0);
                o.SetMaxParallelism(1);
            })
            .Start();

        var clientBus = Configure.With(Using(new BuiltinHandlerActivator()))
            .Transport(t =>
            {
                if (type == "normal")
                {
                    t.UseSqlServerAsOneWayClient(sqlServerTransportOptions);
                }
                else
                {
                    t.UseSqlServerInLeaseModeAsOneWayClient(sqlServerLeaseTransportOptions);
                }
            })
            .Routing(t => t.TypeBased().Map<string>("server"))
            .Start();

        await Task.WhenAll(Enumerable.Range(0, messageCount)
            .InRandomOrder()
            .Select(priority => SendPriMsg(clientBus, priority)));

        serverBus.Advanced.Workers.SetNumberOfWorkers(1);

        counter.WaitForResetEvent();

        await Task.Delay(TimeSpan.FromSeconds(1));

        Assert.That(receivedMessagePriorities.Count, Is.EqualTo(messageCount));
        Assert.That(receivedMessagePriorities.ToArray(), Is.EqualTo(Enumerable.Range(0, messageCount).Reverse().ToArray()));
    }

    static Task SendPriMsg(IBus clientBus, int priority) => clientBus.Send($"prioritet {priority}", new Dictionary<string, string>
    {
        {SqlServerTransport.MessagePriorityHeaderKey, priority.ToString()}
    });
}
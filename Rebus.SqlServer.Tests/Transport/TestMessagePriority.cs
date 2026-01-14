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
public class TestMessagePriority : FixtureBase
{
    protected override void SetUp() => SqlTestHelper.DropAllTables();

    [Test]
    public async Task ReceivedMessagesByPriority_HigherIsMoreImportant_Normal() => await RunTest("normal", false, 20);

    [Test]
    public async Task ReceivedMessagesByPriority_HigherIsMoreImportant_LeaseBased() => await RunTest("lease-based", false, 20);

    [Test]
    public async Task ReceivedMessagesByPriority_HigherIsMoreImportant_NormalSingleMessageTable() => await RunTest("normal", true, 20);

    [Test]
    public async Task ReceivedMessagesByPriority_HigherIsMoreImportant_LeaseBasedSingleMessageTable() => await RunTest("lease-based", true, 20);
    
    async Task RunTest(string type, bool useSingleMessageTable, int messageCount)
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

        var sqlServerTransportOptions = new SqlServerTransportOptions(SqlTestHelper.ConnectionString);
        var sqlServerLeaseTransportOptions = new SqlServerLeaseTransportOptions(SqlTestHelper.ConnectionString);
        if (useSingleMessageTable)
        {
            sqlServerTransportOptions.UseSingleMessageTable("Messages");
            sqlServerLeaseTransportOptions.UseSingleMessageTable("Messages");
        }
        
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
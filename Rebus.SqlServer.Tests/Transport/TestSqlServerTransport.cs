using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Config;
using Rebus.Extensions;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.SqlServer.Transport;
using Rebus.Tests.Contracts;
using Rebus.Threading.TaskParallelLibrary;
using Rebus.Time;
using Rebus.Transport;

namespace Rebus.SqlServer.Tests.Transport;

[TestFixture, Category(Categories.SqlServer)]
public class TestSqlServerTransport : TestSqlServerTransportBase
{
}

[TestFixture, Category(Categories.SqlServer)]
public class TestSingleMessageTableSqlServerTransport : TestSqlServerTransportBase
{
    protected override SqlServerTransportOptions CreateSqlServerTransportOptions(DbConnectionProvider connectionProvider)
    {
        return base.CreateSqlServerTransportOptions(connectionProvider).UseSingleMessageTable("Messages");
    }

    [Test]
    public async Task OnlyReceivesMessagesForCorrectRecipient()
    {
        const string wrongQueueName = "wrong-queue";
        var rebusTime = new DefaultRebusTime();
        var consoleLoggerFactory = new ConsoleLoggerFactory(false);
        var connectionProvider = new DbConnectionProvider(SqlTestHelper.ConnectionString, consoleLoggerFactory);
        var asyncTaskFactory = new TplAsyncTaskFactory(consoleLoggerFactory);

        var sqlServerTransportOptions = CreateSqlServerTransportOptions(connectionProvider);
        
        using var wrongTransport = new SqlServerTransport(connectionProvider, wrongQueueName, consoleLoggerFactory, asyncTaskFactory, rebusTime, sqlServerTransportOptions);

        wrongTransport.EnsureTableIsCreated();
        wrongTransport.Initialize();
        
        using (var scope = new RebusTransactionScope())
        {
            await Transport.Send(QueueName, RecognizableMessage(), scope.TransactionContext);
            await scope.CompleteAsync();
        }
        
        using (var scope = new RebusTransactionScope())
        {
            var transportMessage = await wrongTransport.Receive(scope.TransactionContext, CancellationToken);
            Assert.That(transportMessage, Is.Null, "Other transport received a message that was not for it!");
        }
        
        using (var scope = new RebusTransactionScope())
        {
            var transportMessage = await Transport.Receive(scope.TransactionContext, CancellationToken);
            Assert.That(transportMessage, Is.Not.Null, "Transport did NOT receive the message!");
            AssertMessageIsRecognized(transportMessage);
            await scope.CompleteAsync();
        }
    }
}

public abstract class TestSqlServerTransportBase : FixtureBase
{
    protected const string QueueName = "input";

    protected SqlServerTransport Transport { get; private set; }
    protected CancellationToken CancellationToken { get; private set; }

    protected override void SetUp()
    {
        SqlTestHelper.DropAllTables();

        var rebusTime = new DefaultRebusTime();
        var consoleLoggerFactory = new ConsoleLoggerFactory(false);
        var connectionProvider = new DbConnectionProvider(SqlTestHelper.ConnectionString, consoleLoggerFactory);
        var asyncTaskFactory = new TplAsyncTaskFactory(consoleLoggerFactory);

        var sqlServerTransportOptions = CreateSqlServerTransportOptions(connectionProvider);

        Transport = new SqlServerTransport(connectionProvider, QueueName, consoleLoggerFactory, asyncTaskFactory, rebusTime, sqlServerTransportOptions);

        Using(Transport);

        Transport.EnsureTableIsCreated();
        Transport.Initialize();

        CancellationToken = new CancellationTokenSource().Token;
    }

    protected virtual SqlServerTransportOptions CreateSqlServerTransportOptions(DbConnectionProvider connectionProvider)
    {
        return new SqlServerTransportOptions(connectionProvider);
    }

    [Test]
    public async Task ReceivesSentMessageWhenTransactionIsCommitted()
    {
        using (var scope = new RebusTransactionScope())
        {
            await Transport.Send(QueueName, RecognizableMessage(), scope.TransactionContext);

            await scope.CompleteAsync();
        }

        using (var scope = new RebusTransactionScope())
        {
            var transportMessage = await Transport.Receive(scope.TransactionContext, CancellationToken);

            await scope.CompleteAsync();

            AssertMessageIsRecognized(transportMessage);
        }
    }

    [Test]
    public async Task DoesNotReceiveSentMessageWhenTransactionIsNotCommitted()
    {
        using (var scope = new RebusTransactionScope())
        {
            await Transport.Send(QueueName, RecognizableMessage(), scope.TransactionContext);

            // deliberately skip this:
            //await context.Complete();
        }

        using (var scope = new RebusTransactionScope())
        {
            var transportMessage = await Transport.Receive(scope.TransactionContext, CancellationToken);

            Assert.That(transportMessage, Is.Null);
        }
    }

    [TestCase(1000)]
    public async Task LotsOfAsyncStuffGoingDown(int numberOfMessages)
    {
        var receivedMessages = 0L;
        var messageIds = new ConcurrentDictionary<int, int>();

        Console.WriteLine("Sending {0} messages", numberOfMessages);

        await Task.WhenAll(Enumerable.Range(0, numberOfMessages)
            .Select(async i =>
            {
                using var scope = new RebusTransactionScope();
                await Transport.Send(QueueName, RecognizableMessage(i), scope.TransactionContext);
                await scope.CompleteAsync();
                messageIds[i] = 0;
            }));

        Console.WriteLine("Receiving {0} messages", numberOfMessages);

        using (new Timer(_ => Console.WriteLine("Received: {0} msgs", receivedMessages), null, 0, 1000))
        {
            var stopwatch = Stopwatch.StartNew();

            while (Interlocked.Read(ref receivedMessages) < numberOfMessages && stopwatch.Elapsed < TimeSpan.FromMinutes(2))
            {
                await Task.WhenAll(Enumerable.Range(0, 10).Select(async __ =>
                {
                    using var scope = new RebusTransactionScope();
                    var msg = await Transport.Receive(scope.TransactionContext, CancellationToken);
                    await scope.CompleteAsync();

                    if (msg != null)
                    {
                        Interlocked.Increment(ref receivedMessages);
                        var id = int.Parse(msg.Headers["id"]);
                        messageIds.AddOrUpdate(id, 1, (_, existing) => existing + 1);
                    }
                }));
            }

            await Task.Delay(3000);
        }

        Assert.That(messageIds.Keys.OrderBy(k => k).ToArray(), Is.EqualTo(Enumerable.Range(0, numberOfMessages).ToArray()));

        var kvpsDifferentThanOne = messageIds.Where(kvp => kvp.Value != 1).ToList();

        if (kvpsDifferentThanOne.Any())
        {
            Assert.Fail($@"Oh no! the following IDs were not received exactly once:

{string.Join(Environment.NewLine, kvpsDifferentThanOne.Select(kvp => $"   {kvp.Key}: {kvp.Value}"))}");
        }
    }

    protected void AssertMessageIsRecognized(TransportMessage transportMessage)
    {
        Assert.That(transportMessage.Headers.GetValue("recognizzle"), Is.EqualTo("hej"));
    }

    protected static TransportMessage RecognizableMessage(int id = 0)
    {
        var headers = new Dictionary<string, string>
        {
            {"recognizzle", "hej"},
            {"id", id.ToString()}
        };
        return new TransportMessage(headers, [1, 2, 3]);
    }
}
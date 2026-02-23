using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Transports;

namespace Rebus.SqlServer.Tests.Transport.Contract.Factories;

public class SingleMessageTableSqlServerBusFactory : SqlServerBusFactoryBase
{
    protected override SqlServerTransportOptions CreateSqlServerTransportOptions()
    {
        return base.CreateSqlServerTransportOptions().UseSingleMessageTable("Messages");
    }
}

public class SqlServerBusFactory : SqlServerBusFactoryBase { }

public abstract class SqlServerBusFactoryBase : IBusFactory
{
    readonly List<IDisposable> _stuffToDispose = new List<IDisposable>();

    protected SqlServerBusFactoryBase()
    {
        SqlTestHelper.DropAllTables();
    }

    public IBus GetBus<TMessage>(string inputQueueAddress, Func<TMessage, Task> handler)
    {
        var builtinHandlerActivator = new BuiltinHandlerActivator();

        builtinHandlerActivator.Handle(handler);

        var tableName = "messages" + TestConfig.Suffix;

        SqlTestHelper.DropTable(tableName);

        var bus = Configure.With(builtinHandlerActivator)
            .Transport(t =>
            {
                var sqlServerTransportOptions = CreateSqlServerTransportOptions();
                t.UseSqlServer(sqlServerTransportOptions, inputQueueAddress);
            })
            .Options(o =>
            {
                o.SetNumberOfWorkers(10);
                o.SetMaxParallelism(10);
            })
            .Start();

        _stuffToDispose.Add(bus);

        return bus;
    }

    protected virtual SqlServerTransportOptions CreateSqlServerTransportOptions()
    {
        return new SqlServerTransportOptions(SqlTestHelper.ConnectionString);
    }

    public void Cleanup()
    {
        _stuffToDispose.ForEach(d => d.Dispose());
        _stuffToDispose.Clear();
    }
}
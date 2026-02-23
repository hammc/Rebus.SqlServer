using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Transports;

namespace Rebus.SqlServer.Tests.Transport.Contract.Factories;

public class SqlServerLeaseBusFactory : SqlServerLeaseBusFactoryBase { }

public class SingleMessageTableSqlServerLeaseBusFactory : SqlServerLeaseBusFactoryBase
{
    protected override SqlServerLeaseTransportOptions CreateSqlServerLeaseTransportOptions()
    {
        return base.CreateSqlServerLeaseTransportOptions().UseSingleMessageTable("Messages");
    }
}

public class SqlServerLeaseBusFactoryBase : IBusFactory
{
    readonly List<IDisposable> _stuffToDispose = new List<IDisposable>();

    protected SqlServerLeaseBusFactoryBase()
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
                var sqlServerLeaseTransportOptions = CreateSqlServerLeaseTransportOptions();
                t.UseSqlServerInLeaseMode(sqlServerLeaseTransportOptions,
                    inputQueueAddress);
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

    protected virtual SqlServerLeaseTransportOptions CreateSqlServerLeaseTransportOptions()
    {
        return new SqlServerLeaseTransportOptions(SqlTestHelper.ConnectionString);
    }

    public void Cleanup()
    {
        _stuffToDispose.ForEach(d => d.Dispose());
        _stuffToDispose.Clear();
    }
}
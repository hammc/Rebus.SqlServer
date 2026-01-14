using NUnit.Framework;
using Rebus.SqlServer.Tests.Transport.Contract.Factories;
using Rebus.Tests.Contracts.Transports;

namespace Rebus.SqlServer.Tests.Transport.Contract;

[TestFixture, Category(Categories.SqlServer)]
public class SqlServerTransportBasicSendReceive : BasicSendReceive<SqlTransportFactory>
{
}

[TestFixture, Category(Categories.SqlServer)]
public class SingleMessageTableSqlServerTransportBasicSendReceive : BasicSendReceive<SingleMessageTableSqlTransportFactory>
{
}
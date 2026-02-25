using Microsoft.Data.SqlClient;

namespace Rebus.SqlServer.Transport;

internal interface IMessageTableStrategy
{
    string Address { get; }
    TableName ReceiveTableName { get; }
    string AdditionalCreateColumns { get; }
    string AdditionalPrimaryKeyColumns { get; }
    string AdditionalIndexColumns { get; }
    string AdditionalInsertColumns { get; }
    string AdditionalInsertValues { get; }
    string AdditionalCleanupConditions { get; }
    string AdditionalReceiveConditions { get; }
    void AddAdditionalInsertParameters(SqlCommand command, string destinationAddress);
    void AddAdditionalReceiveParameters(SqlCommand selectCommand);
    void AddAdditionalCleanupParameters(SqlCommand command);
    string SqlDateType { get; }
    string SqlNow { get; }
    TableName GetSendTable(string destinationAddress);
}
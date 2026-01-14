using Microsoft.Data.SqlClient;

namespace Rebus.SqlServer.Transport;

internal class TablePerQueueStrategy : IMessageTableStrategy
{
    public string Address => ReceiveTableName?.QualifiedName;
    public TableName ReceiveTableName { get; }
    public string AdditionalCreateColumns => string.Empty;
    public string AdditionalPrimaryKeyColumns  => string.Empty;
    public string AdditionalIndexColumns => string.Empty;
    public string AdditionalInsertColumns => string.Empty;
    public string AdditionalInsertValues=> string.Empty;
    public string AdditionalCleanupConditions => string.Empty;
    public string AdditionalReceiveConditions => string.Empty;
    
    public TablePerQueueStrategy(string inputQueueName)
    {
        ReceiveTableName = inputQueueName != null ? TableName.Parse(inputQueueName) : null;
    }
    
    public void AddAdditionalInsertParameters(SqlCommand command, string destinationAddress)
    {
    }
    
    public void AddAdditionalReceiveParameters(SqlCommand selectCommand)
    {
    }

    public void AddAdditionalCleanupParameters(SqlCommand command)
    {
    }

    public TableName GetSendTable(string destinationAddress) => TableName.Parse(destinationAddress);
}
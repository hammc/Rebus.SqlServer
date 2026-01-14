using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Rebus.SqlServer.Transport;

internal class SingleMessageTableStrategy : IMessageTableStrategy
{
    private const int RecipientColumnSize = 200;

    public SingleMessageTableStrategy(string inputQueueName, string messageTableName)
    {
        Address = inputQueueName;
        ReceiveTableName = messageTableName != null ? TableName.Parse(messageTableName) : null;
    }

    public string Address { get; }

    public TableName ReceiveTableName { get; }
    public string AdditionalCreateColumns => $"[recipient] [nvarchar]({RecipientColumnSize}) NOT NULL,";
    public string AdditionalPrimaryKeyColumns => "[recipient] ASC,";
    public string AdditionalIndexColumns => "[recipient] ASC,";
    public string AdditionalInsertColumns => "[recipient],";
    public string AdditionalInsertValues => "@recipient,";
    public string AdditionalCleanupConditions => "AND [recipient] = @recipient";
    public string AdditionalReceiveConditions => "AND M.[recipient] = @recipient";

    public void AddAdditionalInsertParameters(SqlCommand command, string destinationAddress)
    {
        command.Parameters.Add("recipient", SqlDbType.NVarChar, RecipientColumnSize).Value = destinationAddress;
    }

    public void AddAdditionalReceiveParameters(SqlCommand selectCommand)
    {
        selectCommand.Parameters.Add("recipient", SqlDbType.NVarChar, RecipientColumnSize).Value = Address;
    }

    public void AddAdditionalCleanupParameters(SqlCommand command)
    {
        command.Parameters.Add("recipient", SqlDbType.NVarChar, RecipientColumnSize).Value = Address;
    }

    public TableName GetSendTable(string destinationAddress) => ReceiveTableName;
}
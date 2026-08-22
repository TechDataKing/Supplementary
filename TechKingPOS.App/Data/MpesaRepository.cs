using System.Collections.Generic;
using System.Data;
using System.Linq;
using TechKingPOS.App.Data;

public static class MpesaRepository
{
    public static List<MpesaMessage> GetUnusedMessages()
    {
        var list = new List<MpesaMessage>();

        using var conn = DbService.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
    SELECT Id, MpesaCode, ExtractedAmount, SenderName, SenderPhone, ReceivedAt
    FROM MpesaMessages
    WHERE IsUsed = 0
    ORDER BY Id DESC";

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            list.Add(new MpesaMessage
            {
                Id = reader.GetInt32(0),
                Code = reader.GetString(1),
                Amount = reader.GetDecimal(2),

                Sender = reader.GetString(3),
                Phone = reader.GetString(4),

                Time = reader.GetDateTime(5)
            });
        }

        return list;
    }

    public static void MarkAsUsed(int id)
    {
        using var conn = DbService.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE MpesaMessages SET IsUsed = 1 WHERE Id = @id";

        cmd.Parameters.AddWithValue("@id", id);

        cmd.ExecuteNonQuery();
    }
}
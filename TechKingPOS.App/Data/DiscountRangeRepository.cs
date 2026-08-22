using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using TechKingPOS.App.Models;
using TechKingPOS.App.Services;
using TechKingPOS.App.Security;
using TechKingPOS.App.Data;
using TechKingPOS.App.Infrastructure;

namespace TechKingPOS.App.Data
{           
            public static class DiscountRangeRepository
            {

            public static List<DiscountRange> GetAll()
        {
            var list = new List<DiscountRange>();

            using var conn = DbService.GetConnection();
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                @"SELECT Id, FromAmount, ToAmount, DiscountPercent, DiscountAmount
                FROM DiscountRanges
                ORDER BY FromAmount ASC";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new DiscountRange
                {
                    Id = reader.GetInt32(0),
                    From = reader.GetDecimal(1),
                    To = reader.GetDecimal(2),
                    
                    // Use null if DB value is NULL
                    DiscountPercent = reader.IsDBNull(3) ? (decimal?)null : reader.GetDecimal(3),
                    DiscountAmount  = reader.IsDBNull(4) ? (decimal?)null : reader.GetDecimal(4)
                });
            }

            return list;
        }


        public static void SaveAll(List<DiscountRange> ranges)
{
    // 🔒 Guard clause — do nothing if null
    if (ranges == null)
        return;

    using var conn = DbService.GetConnection();
    conn.Open();

    using var tx = conn.BeginTransaction();

    try
    {
        using (var clear = conn.CreateCommand())
        {
            clear.Transaction = tx;
            clear.CommandText = "DELETE FROM DiscountRanges";
            clear.ExecuteNonQuery();
        }

        foreach (var r in ranges)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                @"INSERT INTO DiscountRanges
                  (FromAmount, ToAmount, DiscountPercent, DiscountAmount)
                  VALUES (@f, @t, @p, @a)";

            cmd.Parameters.AddWithValue("@f", r.From);
            cmd.Parameters.AddWithValue("@t", r.To);
            cmd.Parameters.AddWithValue("@p", (object?)r.DiscountPercent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@a", (object?)r.DiscountAmount ?? DBNull.Value);

            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        AppState.NotifyDataChanged();

    }
    catch
    {
        tx.Rollback();
        throw;
    }
}

    }
}

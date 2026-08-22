using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using TechKingPOS.App.Models;
using TechKingPOS.App.Services;
using TechKingPOS.App.Security;
using TechKingPOS.App.Data;
using TechKingPOS.App.Infrastructure;

namespace TechKingPOS.App.Data
{   
    public static class DamagedRepository
    {   

        // ================= ADD DAMAGE =================
public static void AddDamage(DamagedItem item)
{
    using var connection = DbService.GetConnection();
    connection.Open();

    using var tx = connection.BeginTransaction();

    try
    {
        decimal beforeQty = 0;

        using var readCmd = connection.CreateCommand();
        readCmd.Transaction = tx;
        readCmd.CommandText = @"
            SELECT Quantity
            FROM Items
            WHERE Id = @id
        ";
        readCmd.Parameters.AddWithValue("@id", item.ItemId);

        var result = readCmd.ExecuteScalar();

        beforeQty = result == DBNull.Value || result == null
            ? 0
            : Convert.ToDecimal(result);

        decimal afterQty = beforeQty - item.Quantity;

        // 1️⃣ Reduce stock
        using var reduceCmd = connection.CreateCommand();
        reduceCmd.Transaction = tx;
        reduceCmd.CommandText = @"
            UPDATE Items
            SET Quantity = Quantity - @qty
            WHERE Id = @id AND BranchId = @branchId
        ";
        reduceCmd.Parameters.AddWithValue("@qty", item.Quantity);
        reduceCmd.Parameters.AddWithValue("@id", item.ItemId);
        reduceCmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);
        reduceCmd.ExecuteNonQuery();

        // 2️⃣ Insert damaged record
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
            INSERT INTO DamagedItems
            (
                ItemId, ItemName, Alias, UnitType,
                Quantity, MarkedPrice, SellingPrice,
                Reason, RecordedBy, DamagedAt, BranchId
            )
            VALUES
            (
                @itemId, @name, @alias, @unitType,
                @qty, @mp, @sp,
                @reason, @by, @date, @branchId
            )
        ";
        cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);
        cmd.Parameters.AddWithValue("@itemId", item.ItemId);
        cmd.Parameters.AddWithValue("@name", item.ItemName);
        cmd.Parameters.AddWithValue("@alias", item.Alias);
        cmd.Parameters.AddWithValue("@unitType", item.UnitType);
        cmd.Parameters.AddWithValue("@qty", item.Quantity);
        cmd.Parameters.AddWithValue("@mp", item.MarkedPrice);
        cmd.Parameters.AddWithValue("@sp", item.SellingPrice);
        cmd.Parameters.AddWithValue("@reason", item.Reason);
        cmd.Parameters.AddWithValue("@by", item.RecordedBy);
        cmd.Parameters.AddWithValue("@date", item.DamagedAt);

        cmd.ExecuteNonQuery();

       
        AppState.NotifyDataChanged();
    ActivityRepository.Log(connection, tx, new Activity
    {
        EntityType = "Item",
        EntityId = item.ItemId,
        EntityName = item.ItemName,

        Action = ActivityActions.DAMAGE,

        QuantityChange = -item.Quantity,

        UnitType = item.UnitType,

        BeforeValue = beforeQty.ToString(),
        AfterValue = afterQty.ToString(),

        Reason = item.Reason,

        PerformedBy = item.RecordedBy,
        BranchId = SessionContext.EffectiveBranchId,
        CreatedAt = DateTime.Now
    });
    tx.Commit();
        LoggerService.Info(
            "⚠️",
            "STOCK",
            "Damaged item recorded",
            $"{item.ItemName} x{item.Quantity}"
        );
    }
    catch
    {
        tx.Rollback();
        throw;
    }
}

        // ================= LOAD ALL =================
        public static List<DamagedItem> GetAll()
        {
            var list = new List<DamagedItem>();

            using var connection = DbService.GetConnection();
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    Id, ItemId, ItemName, Alias, UnitType,
                    Quantity, MarkedPrice, SellingPrice,
                    Reason, RecordedBy, DamagedAt
                FROM DamagedItems
                WHERE BranchId = @branchId
                ORDER BY DamagedAt DESC
            ";
            cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new DamagedItem
                {
                    Id = reader.GetInt32(0),
                    ItemId = reader.GetInt32(1),
                    ItemName = reader.GetString(2),
                    Alias = reader.GetString(3),
                    UnitType = reader.GetString(4),
                    Quantity = reader.GetDecimal(5),
                    MarkedPrice = reader.GetDecimal(6),
                    SellingPrice = reader.GetDecimal(7),
                    Reason = reader.GetString(8),
                    RecordedBy = reader.GetString(9),
                    DamagedAt = reader.GetDateTime(10)
                });
            }

            return list;
        }

        // ================= DELETE (RESTORE STOCK) =================
// ================= DELETE (RESTORE STOCK) =================
public static void Delete(DamagedItem item)
{
    using var connection = DbService.GetConnection();
    connection.Open();

    using var tx = connection.BeginTransaction();

    try
    {
        decimal beforeQty = 0;

        // 👉 READ CURRENT STOCK
        var readCmd = connection.CreateCommand();
        readCmd.Transaction = tx;

        readCmd.CommandText = @"
            SELECT Quantity
            FROM Items
            WHERE Id = @id
              AND BranchId = @branchId
        ";

        readCmd.Parameters.AddWithValue("@id", item.ItemId);
        readCmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);

        var result = readCmd.ExecuteScalar();

        beforeQty = result == null || result == DBNull.Value
            ? 0
            : Convert.ToDecimal(result);

        // 👉 AFTER RESTORE
        decimal afterQty = beforeQty + item.Quantity;

        // 1️⃣ Restore stock
        var restoreCmd = connection.CreateCommand();
        restoreCmd.Transaction = tx;

        restoreCmd.CommandText = @"
            UPDATE Items
            SET Quantity = Quantity + @qty
            WHERE Id = @id
              AND BranchId = @branchId
        ";

        restoreCmd.Parameters.AddWithValue("@qty", item.Quantity);
        restoreCmd.Parameters.AddWithValue("@id", item.ItemId);
        restoreCmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);

        restoreCmd.ExecuteNonQuery();

        // 2️⃣ Delete damaged record
        var deleteCmd = connection.CreateCommand();
        deleteCmd.Transaction = tx;

        deleteCmd.CommandText =
            "DELETE FROM DamagedItems WHERE Id = @id";

        deleteCmd.Parameters.AddWithValue("@id", item.Id);

        deleteCmd.ExecuteNonQuery();

        // 👉 ACTIVITY LOG
        ActivityRepository.Log(connection, tx, new Activity
        {
            EntityType = "Item",
            EntityId = item.ItemId,
            EntityName = item.ItemName,

            Action = "DELETE_DAMAGE",

            QuantityChange = item.Quantity,

            UnitType = item.UnitType,

            BeforeValue = beforeQty.ToString(),
            AfterValue = afterQty.ToString(),

            Reason = $"Damage deleted. Stock restored. Original reason: {item.Reason}",

            PerformedBy = SessionContext.CurrentUserName,

            BranchId = SessionContext.EffectiveBranchId,

            CreatedAt = DateTime.Now
        });

        tx.Commit();

        AppState.NotifyDataChanged();

        LoggerService.Info(
            "♻️",
            "STOCK",
            "Damaged item deleted & stock restored",
            item.ItemName
        );
    }
    catch
    {
        tx.Rollback();
        throw;
    }
}
public static decimal GetTotalDamages(DateTime from, DateTime to)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT ISNULL(SUM(Quantity * MarkedPrice), 0)
        FROM DamagedItems
        WHERE DamagedAt BETWEEN @from AND @to
        AND (@branchId = 0 OR BranchId = @branchId);
    ";

    cmd.Parameters.AddWithValue("@from", from);
    cmd.Parameters.AddWithValue("@to", to);
    cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);

    return Convert.ToDecimal(cmd.ExecuteScalar());
}
    }
}

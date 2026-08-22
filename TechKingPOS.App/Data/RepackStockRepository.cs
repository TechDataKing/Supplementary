using TechKingPOS.App.Data;
using TechKingPOS.App.Security;
using Microsoft.Data.SqlClient;
using TechKingPOS.App.Models;

namespace TechKingPOS.App{

public static class RepackStockRepository
{
public static void Add(SqlConnection con, SqlTransaction tx,RepackStockModel model)
{

    var cmd = con.CreateCommand();
     cmd.Transaction=tx;

    cmd.CommandText = @"
    INSERT INTO RepackStock
    (ItemId, ItemName, SourceQuantity, SourceUnit,
     BaseUnit, TotalQuantity, RemainingQuantity,
     SourceCost, CostPerUnit,
     Status, CreatedAt, CreatedBy, BranchId)
    VALUES
    (@itemId, @name, @sourceQty, @sourceUnit,
     @baseUnit, @total, @remain,
     @sourceCost, @cost,
     'ACTIVE', @date, @user, @branch)";

    cmd.Parameters.AddWithValue("@itemId", model.ItemId);
    cmd.Parameters.AddWithValue("@name", model.ItemName);
    cmd.Parameters.AddWithValue("@sourceQty", model.SourceQuantity);
    cmd.Parameters.AddWithValue("@sourceUnit", model.SourceUnit);

    cmd.Parameters.AddWithValue("@baseUnit", model.BaseUnit);
    cmd.Parameters.AddWithValue("@total", model.TotalQuantity);
    cmd.Parameters.AddWithValue("@remain", model.RemainingQuantity);

    cmd.Parameters.AddWithValue("@sourceCost", model.SourceCost);
    cmd.Parameters.AddWithValue("@cost", model.CostPerUnit);

    cmd.Parameters.AddWithValue("@date", DateTime.Now);
    cmd.Parameters.AddWithValue("@user", SessionContext.CurrentUserName);
    cmd.Parameters.AddWithValue("@branch", SessionContext.EffectiveBranchId);

    cmd.ExecuteNonQuery();
}
public static List<RepackStockModel> GetActive()
{
    var list = new List<RepackStockModel>();

    using var con = DbService.GetConnection();
    con.Open();

    var cmd = con.CreateCommand();
    cmd.CommandText = @"
        SELECT Id,
               ItemId, ItemName,
               SourceQuantity, SourceUnit,
               BaseUnit,
               TotalQuantity, RemainingQuantity,
               SourceCost, CostPerUnit,
               Status,
               CreatedAt, ClosedAt,
               CreatedBy,
               BranchId
        FROM RepackStock
        WHERE Status = 'ACTIVE';
    ";

    using var r = cmd.ExecuteReader();
    while (r.Read())
    {
        list.Add(new RepackStockModel
        {
            Id = (int)r["Id"],

            // SOURCE
            ItemId = (int)r["ItemId"],
            ItemName = r["ItemName"].ToString(),
            SourceQuantity = (decimal)r["SourceQuantity"],
            SourceUnit = r["SourceUnit"].ToString(),

            // OUTPUT
            BaseUnit = r["BaseUnit"].ToString(),
            TotalQuantity = (decimal)r["TotalQuantity"],
            RemainingQuantity = (decimal)r["RemainingQuantity"],

            // COST
            SourceCost = (decimal)r["SourceCost"],
            CostPerUnit = (decimal)r["CostPerUnit"],

            // STATUS
            Status = r["Status"].ToString(),
            CreatedAt = (DateTime)r["CreatedAt"],
            ClosedAt = r["ClosedAt"] == DBNull.Value
                ? null
                : (DateTime?)r["ClosedAt"],

            // TRACE
            CreatedBy = r["CreatedBy"].ToString(),
            BranchId = (int)r["BranchId"]
        });
    }

    return list;
}    public static void Remove(SqlConnection con, SqlTransaction tx, int id)
    {
       

        var cmd = con.CreateCommand();
        cmd.Transaction=tx;

        cmd.CommandText = "DELETE FROM RepackStock WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);

        cmd.ExecuteNonQuery();
    }
    public static void AddStockOnly(
    SqlConnection connection,
    SqlTransaction transaction,
    int itemId,
    decimal quantity)
{
    var cmd = connection.CreateCommand();
    cmd.Transaction = transaction;

    cmd.CommandText = @"
        UPDATE Items
        SET Quantity = Quantity + @qty
        WHERE Id = @id;
    ";

    cmd.Parameters.AddWithValue("@qty", quantity);
    cmd.Parameters.AddWithValue("@id", itemId);

    cmd.ExecuteNonQuery();
}
public static void DeductStock(
    SqlConnection connection,
    SqlTransaction transaction,
    int repackId,
    decimal deductQty,
    string reason,
    string performedBy,
    string action = "REPACK_OUT"
)
{
    decimal beforeQty = 0;
    decimal afterQty = 0;

    // 🔹 1. READ
    using (var cmd = connection.CreateCommand())
    {
        cmd.Transaction = transaction;
        cmd.CommandText = @"
            SELECT RemainingQuantity
            FROM RepackStock
            WHERE Id = @id;
        ";
        cmd.Parameters.AddWithValue("@id", repackId);

        var result = cmd.ExecuteScalar();
        if (result == null)
            throw new Exception("Repack stock not found");

        beforeQty = Convert.ToDecimal(result);
    }

    // 🔹 2. CALCULATE
    afterQty = beforeQty - deductQty;

    if (!SettingsCache.Current.AllowNegativeStock && afterQty < 0)
        throw new InvalidOperationException(
            $"Insufficient repack stock. Available: {beforeQty}"
        );

    // 🔹 3. UPDATE
    using (var cmd = connection.CreateCommand())
    {
        cmd.Transaction = transaction;
        cmd.CommandText = @"
            UPDATE RepackStock
            SET RemainingQuantity = @qty
            WHERE Id = @id;
        ";
        cmd.Parameters.AddWithValue("@qty", afterQty);
        cmd.Parameters.AddWithValue("@id", repackId);
        cmd.ExecuteNonQuery();
    }

    // 🔹 4. LOG ✅
    ActivityRepository.Log(connection, transaction, new Activity
    {
        EntityType = "Repack",
        EntityId = repackId,
        EntityName = $"Repack #{repackId}",
        Action = action,
        QuantityChange = -deductQty,
        BeforeValue = beforeQty.ToString(),
        AfterValue = afterQty.ToString(),
        Reason = reason,
        PerformedBy = performedBy,
        BranchId = SessionContext.EffectiveBranchId,
        CreatedAt = DateTime.Now
    });
}

public static void DeductStockOnly(
    SqlConnection connection,
    SqlTransaction transaction,
    int itemId,
    decimal quantity,
    string reason,
    string action,
    string performedBy)
{
    decimal beforeQty = 0;
    decimal afterQty = 0;
    string name = "";
    string unitType = "";
    decimal? unitValue = null;

    // 🔹 1. READ current data
    using (var cmd = connection.CreateCommand())
    {
        cmd.Transaction = transaction;
        cmd.CommandText = @"
            SELECT Name, Quantity, UnitType, UnitValue
            FROM Items
            WHERE Id = @id;
        ";
        cmd.Parameters.AddWithValue("@id", itemId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            throw new Exception("Item not found");

        name = reader.GetString(0);
        beforeQty = reader.GetDecimal(1);
        unitType = reader.GetString(2);
        unitValue = reader.IsDBNull(3) ? null : reader.GetDecimal(3);
    }

    // 🔹 2. Calculate
    afterQty = beforeQty - quantity;

    if (!SettingsCache.Current.AllowNegativeStock && afterQty < 0)
        throw new InvalidOperationException(
            $"Insufficient stock for {name}. Available: {beforeQty}"
        );

    // 🔹 3. UPDATE
    using (var cmd = connection.CreateCommand())
    {
        cmd.Transaction = transaction;
        cmd.CommandText = @"
            UPDATE Items
            SET Quantity = @qty
            WHERE Id = @id;
        ";
        cmd.Parameters.AddWithValue("@qty", afterQty);
        cmd.Parameters.AddWithValue("@id", itemId);
        cmd.ExecuteNonQuery();
    }

    // 🔹 4. LOG (NOW SAFE ✅)
    ActivityRepository.Log(connection, transaction, new Activity
    {
        EntityType = "Item",
        EntityId = itemId,
        EntityName = name,
        Action = action,
        QuantityChange = -quantity,
        UnitType = unitType,
        UnitValue = unitValue,
        BeforeValue = beforeQty.ToString(),
        AfterValue = afterQty.ToString(),
        Reason = reason,
        PerformedBy = performedBy,
        BranchId = SessionContext.EffectiveBranchId,
        CreatedAt = DateTime.Now
    });
}

public static decimal GetCostPerUnit(
    SqlConnection connection,
    SqlTransaction transaction,
    int repackId)
{
    var cmd = connection.CreateCommand();
    cmd.Transaction = transaction;

    cmd.CommandText = @"
        SELECT CostPerUnit
        FROM RepackStock
        WHERE Id = @id
    ";

    cmd.Parameters.AddWithValue("@id", repackId);

    var result = cmd.ExecuteScalar();

    if (result == null)
        throw new Exception("CostPerUnit not found");

    return Convert.ToDecimal(result);
}
}
}
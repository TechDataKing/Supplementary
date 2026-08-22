using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Threading;
using TechKingPOS.App.Models;
using TechKingPOS.App.Services;
using TechKingPOS.App.Security;
using TechKingPOS.App.Infrastructure;
namespace TechKingPOS.App.Data
{
    public static class ItemRepository
    {  private static long _version = 0;

    public static long Version => _version;
    


    private static void Touch()
    {
        Interlocked.Increment(ref _version);
    }

        // ================= INSERT =================
       public static int InsertItem(
            string name,
            string alias,
            decimal markedPrice,
            decimal sellingPrice,
            decimal? wholesalePrice,
            decimal quantity,
            string unitType,
            decimal? unitValue,
            string? supplier)
        {
            using var connection = DbService.GetConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Items
                        (BranchId, Name, Alias, MarkedPrice, SellingPrice,WholesalePrice, Quantity, UnitType, UnitValue, Supplier, TargetQuantity, CreatedAt)
                    VALUES
                        (@branchId, @name, @alias, @mp, @sp,@wp, @qty, @unitType, @unitValue, @supplier, NULL, @created);
                    SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
                ";

                cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@alias", alias);
                cmd.Parameters.AddWithValue("@mp", markedPrice);
                cmd.Parameters.AddWithValue("@sp", sellingPrice);
                cmd.Parameters.AddWithValue("@wp",
                                wholesalePrice.HasValue ? wholesalePrice.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@qty", quantity);
                cmd.Parameters.AddWithValue("@unitType", unitType);
                cmd.Parameters.AddWithValue("@unitValue", unitValue.HasValue ? unitValue.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@supplier", string.IsNullOrWhiteSpace(supplier) ? DBNull.Value: supplier );
                cmd.Parameters.AddWithValue("@created", DateTime.Now);

                long newItemId = Convert.ToInt64(cmd.ExecuteScalar());
                Touch();
                AppState.NotifyDataChanged();


            LoggerService.Info("💾", "DB", "Item inserted", name);

            ActivityRepository.Log(new Activity
            {
                EntityType = "Item",
                EntityId = (int)newItemId,
                EntityName = name,
                Action = "ADD_ITEM",
                QuantityChange = quantity,
                UnitType = unitType,
                UnitValue = unitValue,
                BeforeValue = "0",
                AfterValue = quantity.ToString(),
                Reason = "New item created",
                PerformedBy = SessionContext.CurrentUserName,
                BranchId = SessionContext.EffectiveBranchId,
                CreatedAt = DateTime.Now
            });
            return (int)newItemId;
            
        } 
        
        
        // ================= LOAD ALL =================
        public static List<ItemLookup> GetAllItems()
        {
            var items = new List<ItemLookup>();

            using var connection = DbService.GetConnection();
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    
                    Id, Name, Alias, Quantity,
                    MarkedPrice, SellingPrice,WholesalePrice,
                    UnitType, TargetQuantity, Supplier
                FROM Items
                WHERE  BranchId = @branchId
                ORDER BY Name ASC;
            ";
            cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                items.Add(ReadItem(reader));
            }

            return items;
        }

        // ================= SEARCH =================
        public static List<ItemLookup> SearchItems(string text)
        {
            var items = new List<ItemLookup>();

            using var connection = DbService.GetConnection();
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    Id, Name, Alias, Quantity,
                    MarkedPrice, SellingPrice,WholesalePrice,
                    UnitType, TargetQuantity, Supplier
                FROM Items
                WHERE
                BranchId = @branchId 
                AND (Name LIKE @q OR Alias LIKE @q)
                ORDER BY Name ASC;
            ";
            cmd.Parameters.AddWithValue("@q", $"%{text}%");
            cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                items.Add(ReadItem(reader));
            }

            return items;
        }

        // ================= TARGET TAB =================
        public static List<ItemLookup> GetItemsWithoutTarget(string text)
        {
            var items = new List<ItemLookup>();

            using var connection = DbService.GetConnection();
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT
                    Id, Name, Quantity
                FROM Items
                WHERE TargetQuantity IS NULL
                AND Name LIKE @q
                ORDER BY Name ASC;
            ";
            cmd.Parameters.AddWithValue("@q", $"%{text}%");

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                items.Add(new ItemLookup
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Quantity = reader.GetDecimal(2)
                });
            }

            return items;
        }
public static ItemModel? GetByNameOrAlias(string name, string? alias)
{
    using var connection = DbService.GetConnection();
    connection.Open();

    using var cmd = connection.CreateCommand();
    cmd.CommandText = @"
        SELECT TOP 1
            Id, Name, Alias,
            MarkedPrice, SellingPrice,WholesalePrice,
            Quantity, UnitType, UnitValue, Supplier
        FROM Items
        WHERE BranchId = @branchId
            AND (LOWER(Name) = LOWER(@name)
                OR (Alias IS NOT NULL AND LOWER(Alias) = LOWER(@alias)))

        ;
    ";
    cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);
    cmd.Parameters.AddWithValue("@name", name.Trim());
    cmd.Parameters.AddWithValue(
        "@alias",
        string.IsNullOrWhiteSpace(alias) ? DBNull.Value : alias.Trim()
    );

    using var reader = cmd.ExecuteReader();

    if (!reader.Read())
        return null;

    return new ItemModel
    {
        Id = reader.GetInt32(0),
        Name = reader.GetString(1),
        Alias = reader.IsDBNull(2) ? "" : reader.GetString(2),
        MarkedPrice = reader.GetDecimal(3),
        SellingPrice = reader.GetDecimal(4),
        WholesalePrice = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
        Quantity = reader.GetDecimal(6),
        UnitType = reader.GetString(7),
        UnitValue = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
        Supplier = reader.IsDBNull(9) ? null : reader.GetString(9)
       
    };
}
public static ItemModel? GetById(int id)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"SELECT Id, Name, Alias, MarkedPrice, SellingPrice, UnitType 
                        FROM Items 
                        WHERE Id = @id";

    cmd.Parameters.AddWithValue("@id", id);

    using var reader = cmd.ExecuteReader();

    if (!reader.Read())
        return null;

    return new ItemModel
    {
        Id = reader.GetInt32(0),
        Name = reader.GetString(1),
        Alias = reader.IsDBNull(2) ? null : reader.GetString(2),
        MarkedPrice = reader.GetDecimal(3),
        SellingPrice = reader.GetDecimal(4),
        UnitType = reader.GetString(5)
    };
}
// ================= ADD STOCK =================
public static void AddStock(
    int itemId,
    decimal addQuantity,
    decimal markedPrice,
    decimal sellingPrice,
    decimal? wholesalePrice,
    string action)
{
    using var connection = DbService.GetConnection();
    connection.Open();

    decimal beforeQty = 0;
    decimal afterQty = 0;

    string name = "";
    string unitType = "";
    decimal? unitValue = null;

    // READ CURRENT STOCK
    using (var readCmd = connection.CreateCommand())
    {
        readCmd.CommandText = @"
            SELECT Name, Quantity, UnitType, UnitValue
            FROM Items
            WHERE Id = @id;
        ";

        readCmd.Parameters.AddWithValue("@id", itemId);

        using var reader = readCmd.ExecuteReader();

        if (reader.Read())
        {
            name = reader.GetString(0);

            // SAFE DB VALUE
            beforeQty = reader.IsDBNull(1)
                ? 0
                : reader.GetDecimal(1);

            unitType = reader.GetString(2);

            unitValue = reader.IsDBNull(3)
                ? null
                : reader.GetDecimal(3);
        }
    }

    afterQty = beforeQty + addQuantity;

    // UPDATE STOCK
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = @"
            UPDATE Items
            SET
                Quantity = @qty,
                MarkedPrice = @marked,
                SellingPrice = @selling,
                WholesalePrice = @wholesale
            WHERE Id = @id;
        ";

        cmd.Parameters.AddWithValue("@qty", afterQty);
        cmd.Parameters.AddWithValue("@marked", markedPrice);
        cmd.Parameters.AddWithValue("@selling", sellingPrice);

        cmd.Parameters.AddWithValue("@wholesale",wholesalePrice.HasValue
                ? wholesalePrice.Value
                : DBNull.Value
        );

        cmd.Parameters.AddWithValue("@id", itemId);

        cmd.ExecuteNonQuery();
    }

    Touch();
    AppState.NotifyDataChanged();

    // ACTIVITY
    ActivityRepository.Log(new Activity
    {
        EntityType = "Item",
        EntityId = itemId,
        EntityName = name,
        Action = action,

        QuantityChange = addQuantity,

        UnitType = unitType,
        UnitValue = unitValue,

        BeforeValue = beforeQty.ToString(),
        AfterValue = afterQty.ToString(),

        Reason = "Stock added",

        PerformedBy = SessionContext.CurrentUserName,
        BranchId = SessionContext.EffectiveBranchId,
        CreatedAt = DateTime.Now
    });
}

        // ================= UPDATE ITEM =================
         public static void UpdateItem(
            int id,
            string name,
            string alias,
            decimal quantity,
            decimal markedPrice,
            decimal? wholesalePrice,
            decimal sellingPrice,
            decimal? targetQuantity,
            string action)
        {
            using var connection = DbService.GetConnection();
            connection.Open();

            decimal beforeQty = 0;

            using (var readCmd = connection.CreateCommand())
            {
                readCmd.CommandText = "SELECT Quantity FROM Items WHERE Id = @id;";
                readCmd.Parameters.AddWithValue("@id", id);
                using var reader = readCmd.ExecuteReader();
                if (reader.Read())
                    beforeQty = reader.GetDecimal(0);
            }

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE Items SET
                    Name = @name,
                    Alias = @alias,
                    Quantity = @qty,
                    MarkedPrice = @mp,
                    WholesalePrice=@wp,
                    SellingPrice = @sp,
                    TargetQuantity = @target
                WHERE Id = @id;
            ";

            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@alias", alias);
            cmd.Parameters.AddWithValue("@qty", quantity);
            cmd.Parameters.AddWithValue("@mp", markedPrice);
            cmd.Parameters.AddWithValue("@sp", sellingPrice);
            cmd.Parameters.AddWithValue("@wp", 
                 wholesalePrice.HasValue ? wholesalePrice.Value
                 :DBNull.Value  );
            cmd.Parameters.AddWithValue(
                "@target",
                targetQuantity.HasValue ? targetQuantity.Value : DBNull.Value
            );
            cmd.Parameters.AddWithValue("@id", id);

            cmd.ExecuteNonQuery();
            Touch();
            AppState.NotifyDataChanged();

            ActivityRepository.Log(new Activity
            {
                EntityType = "Item",
                EntityId = id,
                EntityName = name,
                Action = action,
                QuantityChange = quantity - beforeQty,
                BeforeValue = beforeQty.ToString(),
                AfterValue = quantity.ToString(),
                Reason = "Item updated",
                PerformedBy = SessionContext.CurrentUserName,
                BranchId = SessionContext.EffectiveBranchId,
                CreatedAt = DateTime.Now
            });
        }

        // ================= SET TARGET =================
        public static void SetTarget(int id, decimal? target)
        {
            using var connection = DbService.GetConnection();
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE Items SET TargetQuantity = @target WHERE Id = @id;
            ";

            cmd.Parameters.AddWithValue("@target", target);
            cmd.Parameters.AddWithValue("@id", id);

            cmd.ExecuteNonQuery();
            ActivityRepository.Log(new Activity
                {
                    EntityType = "Item",
                    EntityId = id,
                    Action = "SET_TARGET",
                    QuantityChange = 0,                     // ✔ explicit
                    AfterValue = target.ToString(),
                    Reason = "Target quantity set",
                     PerformedBy = SessionContext.CurrentUserName,
                    BranchId = SessionContext.EffectiveBranchId,
                    CreatedAt = DateTime.Now
                });

        }

        // ================= DELETE =================
        public static void DeleteItem(int id)
        {
            using var connection = DbService.GetConnection();
            connection.Open();

            string name = "";
            decimal qty = 0;
            string unitType = "";
            decimal? unitValue = null;

            using (var readCmd = connection.CreateCommand())
            {
                readCmd.CommandText = @"
                    SELECT Name, Quantity, UnitType, UnitValue
                    FROM Items WHERE Id = @id;
                ";
                readCmd.Parameters.AddWithValue("@id", id);

                using var reader = readCmd.ExecuteReader();
                if (reader.Read())
                {
                    name = reader.GetString(0);
                    qty = reader.IsDBNull(1)
                        ? 0
                        : reader.GetDecimal(1);
                    unitType = reader.GetString(2);
                    unitValue = reader.IsDBNull(3) ? null : reader.GetDecimal(3);
                }
            }

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Items WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            Touch();
            AppState.NotifyDataChanged();

            ActivityRepository.Log(new Activity
            {
                EntityType = "Item",
                EntityId = id,
                EntityName = name,
                Action = "DELETE_ITEM",
                QuantityChange = -qty,
                UnitType = unitType,
                UnitValue = unitValue,
                BeforeValue = qty.ToString(),
                AfterValue = " 0 ",
                Reason = "Item deleted",
                PerformedBy = SessionContext.CurrentUserName,
                BranchId = SessionContext.EffectiveBranchId,
                CreatedAt = DateTime.Now
            });
        }

        // ================= HELPER =================
        private static ItemLookup ReadItem(SqlDataReader reader)
        {
            return new ItemLookup
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Alias = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Quantity = reader.GetDecimal(3),
                MarkedPrice = reader.GetDecimal(4),
                SellingPrice = reader.GetDecimal(5),
                WholesalePrice = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                UnitType = reader.GetString(7),
                TargetQuantity = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                Supplier = reader.IsDBNull(9) ? null : reader.GetString(9),
            };
        }
public static decimal GetBaseQuantity(
    SqlConnection conn,
    SqlTransaction tx,
    int itemId)
{
    var cmd = conn.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = @"
        SELECT Quantity
        FROM Items
        WHERE Id = @id
          AND BranchId = @bid;
    ";
    cmd.Parameters.AddWithValue("@id", itemId);
    cmd.Parameters.AddWithValue("@bid", SessionContext.EffectiveBranchId);

    return Convert.ToDecimal(cmd.ExecuteScalar());
}

        public static void DeductStock(
            SqlConnection connection,
            SqlTransaction transaction,
            int itemId,
            decimal deductQuantity,
            string reason,
            string performedBy,
            string action = ActivityActions.STOCK_OUT)
        {
            decimal beforeQty = 0;
            string name = "";
            string unitType = "";
            decimal? unitValue = null;

            // READ current stock (LOCKED by transaction)
            using (var readCmd = connection.CreateCommand())
            {
                readCmd.Transaction = transaction;
                readCmd.CommandText = @"
                    SELECT Name, Quantity, UnitType, UnitValue
                    FROM Items
                    WHERE Id = @id;
                ";
                readCmd.Parameters.AddWithValue("@id", itemId);

                using var reader = readCmd.ExecuteReader();
                if (!reader.Read())
                    throw new Exception("Item not found during stock deduction");

                name = reader.GetString(0);
                beforeQty = reader.GetDecimal(1);
                unitType = reader.GetString(2);
                unitValue = reader.IsDBNull(3) ? null : reader.GetDecimal(3);
            }

            decimal afterQty = beforeQty - deductQuantity;

            // ❗ Enforce NO negative stock if disabled
            if (!SettingsCache.Current.AllowNegativeStock && afterQty < 0)
                throw new InvalidOperationException(
                    $"Insufficient stock for {name}. Available: {beforeQty}"
                );

            // UPDATE stock
            using (var updateCmd = connection.CreateCommand())
            {
                updateCmd.Transaction = transaction;
                updateCmd.CommandText = @"
                    UPDATE Items
                    SET Quantity = @qty
                    WHERE Id = @id;
                ";
                updateCmd.Parameters.AddWithValue("@qty", afterQty);
                updateCmd.Parameters.AddWithValue("@id", itemId);
                updateCmd.ExecuteNonQuery();

                    Touch();
                    AppState.NotifyDataChanged();

            }

            // ACTIVITY (same transaction-safe connection)
            ActivityRepository.Log(
                connection,
                transaction,
                new Activity
                {
                    EntityType = "Item",
                    EntityId = itemId,
                    EntityName = name,
                    Action = action,
                    QuantityChange = -deductQuantity,
                    UnitType = unitType,
                    UnitValue = unitValue,
                    BeforeValue = beforeQty.ToString(),
                    AfterValue = afterQty.ToString(),
                    Reason = reason,
                    PerformedBy = performedBy,
                    BranchId = SessionContext.EffectiveBranchId,
                    CreatedAt = DateTime.Now
                }
            );
        }
        public static decimal GetMarkedPrice(
    SqlConnection connection,
    SqlTransaction transaction,
    int itemId)
{
    using var cmd = connection.CreateCommand();
    cmd.Transaction = transaction;
    cmd.CommandText = @"
        SELECT MarkedPrice
        FROM Items
        WHERE Id = @id
          AND BranchId = @branchId;
    ";

    cmd.Parameters.AddWithValue("@id", itemId);
    cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);

    object? result = cmd.ExecuteScalar();

    if (result == null || result == DBNull.Value)
        throw new InvalidOperationException("Marked price not found for item");

    return Convert.ToDecimal(result);
}

// ================= DEDUCT STOCK FROM LOCATION =================
public static void DeductStockFromLocation(
    SqlConnection conn,
    SqlTransaction tx,
    int itemId,
    int locationId,
    decimal quantity,
    string reason,
    string performedBy)
{
    decimal beforeQty;
    string name = "";

    // READ current stock at location
    using (var cmd = conn.CreateCommand())
    {
        cmd.Transaction = tx;
        cmd.CommandText = @"
            SELECT i.Name, sl.Quantity
            FROM StockLevels sl
            INNER JOIN Items i ON i.Id = sl.ItemId
            WHERE sl.ItemId = @itemId AND sl.LocationId = @locId;
        ";
        cmd.Parameters.AddWithValue("@itemId", itemId);
        cmd.Parameters.AddWithValue("@locId", locationId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException("Stock record not found for this location");

        name = reader.GetString(0);
        beforeQty = reader.GetDecimal(1);

        if (!SettingsCache.Current.AllowNegativeStock && beforeQty < quantity)
            throw new InvalidOperationException($"Insufficient stock at {locationId} for {name}");
    }

    // UPDATE stock
    using (var cmd = conn.CreateCommand())
    {
        cmd.Transaction = tx;
        cmd.CommandText = @"
            UPDATE StockLevels
            SET Quantity = Quantity - @qty, UpdatedAt = SYSDATETIME()
            WHERE ItemId = @itemId AND LocationId = @locId;
        ";
        cmd.Parameters.AddWithValue("@qty", quantity);
        cmd.Parameters.AddWithValue("@itemId", itemId);
        cmd.Parameters.AddWithValue("@locId", locationId);
        cmd.ExecuteNonQuery();
    }

    // LOG activity
    ActivityRepository.Log(
        conn,
        tx,
        new Activity
        {
            EntityType = "Item",
            EntityId = itemId,
            EntityName = name,
            Action = "STOCK_OUT",
            QuantityChange = -quantity,
            Reason = reason,
            PerformedBy = performedBy,
            BranchId = SessionContext.EffectiveBranchId,
            CreatedAt = DateTime.Now
        }
    );
}

// ================= ADD STOCK TO LOCATION =================
public static void AddStockToLocation(
    SqlConnection conn,
    SqlTransaction tx,
    int itemId,
    int locationId,
    decimal quantity)
{
    // Ensure stock record exists
    using var cmdCheck = conn.CreateCommand();
    cmdCheck.Transaction = tx;
    cmdCheck.CommandText = @"
        IF NOT EXISTS (SELECT 1 FROM StockLevels WHERE ItemId = @itemId AND LocationId = @locId)
            INSERT INTO StockLevels(ItemId, LocationId, Quantity) VALUES(@itemId, @locId, 0);
    ";
    cmdCheck.Parameters.AddWithValue("@itemId", itemId);
    cmdCheck.Parameters.AddWithValue("@locId", locationId);
    cmdCheck.ExecuteNonQuery();

    // Add quantity
    using var cmd = conn.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = @"
        UPDATE StockLevels
        SET Quantity = Quantity + @qty, UpdatedAt = SYSDATETIME()
        WHERE ItemId = @itemId AND LocationId = @locId;
    ";
    cmd.Parameters.AddWithValue("@qty", quantity);
    cmd.Parameters.AddWithValue("@itemId", itemId);
    cmd.Parameters.AddWithValue("@locId", locationId);
    cmd.ExecuteNonQuery();
}

// ================= LOG STOCK MOVEMENT =================
public static void LogStockMovement(
    SqlConnection conn,
    SqlTransaction tx,
    int itemId,
    int fromLocationId,
    int toLocationId,
    decimal quantity,
    string reason,
    string performedBy)
{
    using var cmd = conn.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = @"
        INSERT INTO StockMovements
        (ItemId, Quantity, FromLocationId, ToLocationId, Reason, PerformedBy)
        VALUES
        (@itemId, @qty, @fromId, @toId, @reason, @performedBy);
    ";
    cmd.Parameters.AddWithValue("@itemId", itemId);
    cmd.Parameters.AddWithValue("@qty", quantity);
    cmd.Parameters.AddWithValue("@fromId", fromLocationId);
    cmd.Parameters.AddWithValue("@toId", toLocationId);
    cmd.Parameters.AddWithValue("@reason", reason ?? "");
    cmd.Parameters.AddWithValue("@performedBy", performedBy);
    cmd.ExecuteNonQuery();
}
public static decimal GetUnitValue(
    SqlConnection conn,
    SqlTransaction tx,
    int itemId)
{
    var cmd = conn.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = @"
        SELECT UnitValue
        FROM Items
        WHERE Id = @id
        AND BranchId = @branchId";

    cmd.Parameters.AddWithValue("@id", itemId);
    cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);

    var result = cmd.ExecuteScalar();

    return result == DBNull.Value || result == null
        ? 1m
        : Convert.ToDecimal(result);
}
public static ItemModel? GetExactItem(
    string name,
    string? alias)
{
    using var connection = DbService.GetConnection();

    connection.Open();

    using var cmd = connection.CreateCommand();

    cmd.CommandText = @"
        SELECT TOP 1
            Id,
            Name,
            Alias,
            MarkedPrice,
            SellingPrice,
            WholesalePrice,
            Quantity,
            UnitType,
            UnitValue,
            Supplier
        FROM Items
        WHERE
            BranchId = @branchId

            AND LOWER(Name) = LOWER(@name)

            AND ISNULL(LOWER(Alias), '') =
                ISNULL(LOWER(@alias), '')
    ";

    cmd.Parameters.AddWithValue(
        "@branchId",
        SessionContext.EffectiveBranchId
    );

    cmd.Parameters.AddWithValue(
        "@name",
        name.Trim()
    );

    cmd.Parameters.AddWithValue(
        "@alias",
        string.IsNullOrWhiteSpace(alias)
            ? ""
            : alias.Trim()
    );

    using var reader = cmd.ExecuteReader();

    if (!reader.Read())
        return null;

    return new ItemModel
    {
        Id = reader.GetInt32(0),
        Name = reader.GetString(1),
        Alias = reader.IsDBNull(2)
            ? ""
            : reader.GetString(2),

        MarkedPrice = reader.GetDecimal(3),
        SellingPrice = reader.GetDecimal(4),

        WholesalePrice = reader.IsDBNull(5)
            ? null
            : reader.GetDecimal(5),

        Quantity = reader.GetDecimal(6),

        UnitType = reader.GetString(7),

        UnitValue = reader.IsDBNull(8)
            ? null
            : reader.GetDecimal(8),

        Supplier = reader.IsDBNull(9)
            ? null
            : reader.GetString(9)
    };
}

    }
}

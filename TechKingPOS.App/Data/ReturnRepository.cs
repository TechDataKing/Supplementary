
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using TechKingPOS.App.Models;
using  TechKingPOS.App.Security;
using TechKingPOS.App.Services;

namespace TechKingPOS.App.Data
{
    public static class ReturnsRepository
    {
public static void InsertReturn(
    string? receiptNumber,
    int itemId,
    string itemName,
    string? alias,
    decimal quantity,
    decimal unitPrice,
    decimal price,
    string reason,
    string returnType,
    string performedBy,
    int branchId,
    string? supplierName = null,
    DateTime? saleDate = null,
    string? refundMethod = null,
    bool restock = true)
{
    using var connection = DbService.GetConnection();
    connection.Open();

    using var tx = connection.BeginTransaction();

    try
    {
        // ================= INSERT RETURN =================
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;

        cmd.CommandText = @"
        INSERT INTO Returns
        (
            ReceiptNumber,
            SaleDate,
            ReturnDate,
            ItemId,
            ItemName,
            Alias,
            Quantity,
            UnitPrice,
            Price,
            Reason,
            ReturnType,
            BranchId,
            PerformedBy,
            RefundMethod,
            Restock,
            SupplierName
        )
        VALUES
        (
            @receiptNumber,
            @saleDate,
            SYSDATETIME(),
            @itemId,
            @itemName,
            @alias,
            @qty,
            @unitPrice,
            @price,
            @reason,
            @returnType,
            @branchId,
            @performedBy,
            @refundMethod,
            @restock,
            @supplierName
        );
        ";

        cmd.Parameters.AddWithValue("@receiptNumber", receiptNumber ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@saleDate", saleDate ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@itemId", itemId);
        cmd.Parameters.AddWithValue("@itemName", itemName);
        cmd.Parameters.AddWithValue("@alias", alias ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@qty", quantity);
        cmd.Parameters.AddWithValue("@unitPrice", unitPrice);
        cmd.Parameters.AddWithValue("@price", price);
        cmd.Parameters.AddWithValue("@reason", reason ?? "");
        cmd.Parameters.AddWithValue("@returnType", returnType);
        cmd.Parameters.AddWithValue("@branchId", branchId);
        cmd.Parameters.AddWithValue("@performedBy", performedBy);
        cmd.Parameters.AddWithValue("@refundMethod", refundMethod ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@restock", restock);
        cmd.Parameters.AddWithValue("@supplierName", supplierName ?? (object)DBNull.Value);

        cmd.ExecuteNonQuery();

        // ================= 1️⃣ ALWAYS LOG RETURN EVENT =================
        ActivityRepository.Log(connection, tx, new Activity
        {
            EntityType = "Item",
            EntityId = itemId,
            EntityName = itemName,
            Action = returnType == "Customer Return"
                ? "CUSTOMER_RETURN"
                : "SUPPLIER_RETURN",
            QuantityChange = -quantity, // movement OUT of customer/supplier
            UnitValue = quantity,
            Price = price,
            Reason = reason,
            PerformedBy = performedBy,
            BranchId = branchId,
            CreatedAt = DateTime.Now
        });


        tx.Commit();
    }
    catch
    {
        tx.Rollback();
        throw;
    }
}       

        // ================= SEARCH SUPPLIER ITEMS =================
public static List<ItemLookup> SearchItems(string text)
{
    var items = new List<ItemLookup>();

    using var connection = DbService.GetConnection();
    connection.Open();

    var cmd = connection.CreateCommand();
    cmd.CommandText = @"
        SELECT
            Id, Name, Alias, Quantity,
            MarkedPrice, SellingPrice,
            UnitType, TargetQuantity, Supplier
        FROM Items
        WHERE (Name LIKE @q OR Alias LIKE @q)
        AND BranchId = @branchId
        ORDER BY Name ASC;
    ";

    cmd.Parameters.AddWithValue("@q", $"%{text}%");
    cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);

    using var reader = cmd.ExecuteReader();

while (reader.Read())
{
    items.Add(new ItemLookup
    {
        Id = reader.GetInt32(reader.GetOrdinal("Id")),

        Name = reader.IsDBNull(reader.GetOrdinal("Name"))
            ? ""
            : reader.GetString(reader.GetOrdinal("Name")),

        Alias = reader.IsDBNull(reader.GetOrdinal("Alias"))
            ? ""
            : reader.GetString(reader.GetOrdinal("Alias")),

        Quantity = reader.IsDBNull(reader.GetOrdinal("Quantity"))
            ? 0
            : reader.GetDecimal(reader.GetOrdinal("Quantity")),

        Supplier = reader.IsDBNull(reader.GetOrdinal("Supplier"))
            ? ""
            : reader.GetString(reader.GetOrdinal("Supplier")),

        SellingPrice = reader.IsDBNull(reader.GetOrdinal("SellingPrice"))
            ? 0
            : reader.GetDecimal(reader.GetOrdinal("SellingPrice")),

        MarkedPrice = reader.IsDBNull(reader.GetOrdinal("MarkedPrice"))
            ? 0
            : reader.GetDecimal(reader.GetOrdinal("MarkedPrice")),

        UnitType = reader.IsDBNull(reader.GetOrdinal("UnitType"))
            ? ""
            : reader.GetString(reader.GetOrdinal("UnitType")),

        TargetQuantity = reader.IsDBNull(reader.GetOrdinal("TargetQuantity"))
            ? 0
            : reader.GetDecimal(reader.GetOrdinal("TargetQuantity"))
    });
}
    

    return items;
}// ================= GET SUPPLIERS =================
public static List<string> GetSuppliers(string search = "")
{
    var list = new List<string>();

    using var connection = DbService.GetConnection();
    connection.Open();

    using var cmd = connection.CreateCommand();
    cmd.CommandText = @"
        SELECT DISTINCT Supplier
        FROM Items
        WHERE Supplier IS NOT NULL
        AND Supplier LIKE @search
        ORDER BY Supplier ASC;
    ";

    cmd.Parameters.AddWithValue("@search", $"%{search}%");

    using var reader = cmd.ExecuteReader();

    while (reader.Read())
        list.Add(reader.GetString(0));

    return list;
}
public static List<SoldItemReport> GetSoldItems(
            DateTime from,
            DateTime to, int branchId)
        {
            using var connection = DbService.GetConnection();
            connection.Open();

            LoggerService.Info(
                "📦",
                "REPORT",
                "Loading sold items",
                $"{from:yyyy-MM-dd} → {to:yyyy-MM-dd}"
            );

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            SELECT
                s.ReceiptNumber,
                s.ItemName,
                SUM(s.Quantity) AS Quantity,
                s.Price,
                SUM(s.Total) AS Total,
                MIN(s.CreatedAt) AS CreatedAt,
                t.Cashier,
                ISNULL(pm.PaymentMethod, 'CREDIT') AS PaymentMethod
            FROM Sales s
            JOIN Transactions t 
                ON t.ReceiptNumber = s.ReceiptNumber
            LEFT JOIN (
                SELECT 
                    ReceiptNumber,
                    STRING_AGG(Method, ', ') AS PaymentMethod
                FROM Payments
                GROUP BY ReceiptNumber
            ) pm ON pm.ReceiptNumber = s.ReceiptNumber
            WHERE t.CreatedAt BETWEEN @from AND @to
            AND t.BranchId = @branchId
            GROUP BY 
                s.ReceiptNumber,
                s.ItemName,
                s.Price,
                t.Cashier,
                pm.PaymentMethod
            ORDER BY MIN(s.CreatedAt) ASC;
            ";

            cmd.Parameters.AddWithValue("@from", from);
            cmd.Parameters.AddWithValue("@to", to);
            cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);

            var result = new List<SoldItemReport>();

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new SoldItemReport
                {
                    ReceiptNumber = reader.GetString(reader.GetOrdinal("ReceiptNumber")),
                    ItemName      = reader.GetString(reader.GetOrdinal("ItemName")),
                    Quantity      = reader.GetDecimal(reader.GetOrdinal("Quantity")),
                    Price         = reader.GetDecimal(reader.GetOrdinal("Price")),
                    Total         = reader.GetDecimal(reader.GetOrdinal("Total")),
                    PaymentMethod = reader["PaymentMethod"]?.ToString(),
                    Cashier   = reader["Cashier"]?.ToString()
                });
            }

            LoggerService.Info(
                "✅",
                "REPORT",
                "Sold items loaded",
                $"Items={result.Count}"
            );

            return result;
        }

public static decimal GetReturnedQty(int itemId, string receipt)
{
    using var connection = DbService.GetConnection();
    connection.Open();

    var cmd = connection.CreateCommand();
    cmd.CommandText = @"
        SELECT ISNULL(SUM(Quantity), 0)
        FROM Returns
        WHERE ItemId = @itemId
        AND ReceiptNumber = @receipt
    ";

    cmd.Parameters.AddWithValue("@itemId", itemId);
    cmd.Parameters.AddWithValue("@receipt", receipt);

    return Convert.ToDecimal(cmd.ExecuteScalar());
}
public static List<SoldItemReport> GetItemsByReceipt(string receiptNumber)
{
    var list = new List<SoldItemReport>();

    using var connection = DbService.GetConnection();
    connection.Open();

    var cmd = connection.CreateCommand();
    cmd.CommandText = @"
SELECT 
    ItemId,
    ItemName,
    SUM(Quantity) AS Quantity,
    Price,
    CostPrice
FROM Sales
WHERE ReceiptNumber = @receipt
GROUP BY 
    ItemId, 
    ItemName, 
    Price, 
    CostPrice;
    ";

    cmd.Parameters.AddWithValue("@receipt", receiptNumber);

    using var reader = cmd.ExecuteReader();

    while (reader.Read())
    {
list.Add(new SoldItemReport
{
    ItemId = reader.GetInt32(0),
    ItemName = reader.GetString(1),
    Quantity = reader.GetDecimal(2),
    Price = reader.GetDecimal(3),       // selling price
    CostPrice = reader.GetDecimal(4)    // cost price
});
    }

    return list;
}
public static List<string> GetReceipts(string search = "")
{
    var list = new List<string>();

    using var connection = DbService.GetConnection();
    connection.Open();

    var cmd = connection.CreateCommand();
    cmd.CommandText = @"
        SELECT DISTINCT ReceiptNumber
        FROM Sales
        WHERE ReceiptNumber LIKE @search
        ORDER BY ReceiptNumber DESC
    ";

    cmd.Parameters.AddWithValue("@search", $"%{search}%");

    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        list.Add(reader.GetString(0));
    }

    return list;
}
    }
}

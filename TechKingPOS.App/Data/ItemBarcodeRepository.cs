using Microsoft.Data.SqlClient;
using TechKingPOS.App.Services;

using TechKingPOS.App.Models;
using TechKingPOS.App.Security;

namespace TechKingPOS.App.Data
{
    public static class ItemBarcodeRepository
    {
        public static ItemBarcode? GetByBarcode(string barcode)
        {
            using var conn = DbService.GetConnection();
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT ItemId, UnitsPerScan
                FROM ItemBarcodes
                WHERE Barcode = @barcode
            ";
            cmd.Parameters.AddWithValue("@barcode", barcode);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            return new ItemBarcode
            {
                ItemId = r.GetInt32(0),
                UnitsPerScan = r.GetDecimal(1)
            };
        }

public static bool Insert(string barcode, int itemId, decimal unitsPerScan, out string message)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    // 🔍 CHECK FIRST
    using (var check = conn.CreateCommand())
    {
        check.CommandText = "SELECT COUNT(*) FROM ItemBarcodes WHERE Barcode = @b";
        check.Parameters.AddWithValue("@b", barcode);

        var exists = (int)check.ExecuteScalar() > 0;

        if (exists)
        {
            message = "❌ Barcode already exists";
            return false;
        }
    }

    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        INSERT INTO ItemBarcodes (Barcode, ItemId, UnitsPerScan)
        VALUES (@b, @i, @u)
    ";

    cmd.Parameters.AddWithValue("@b", barcode);
    cmd.Parameters.AddWithValue("@i", itemId);
    cmd.Parameters.AddWithValue("@u", unitsPerScan);

    cmd.ExecuteNonQuery();

    message = "✔ Barcode mapped";
    return true;
}
public static List<string> SearchBarcodes(string text)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT Barcode
        FROM ItemBarcodes
        WHERE Barcode LIKE @t
        ORDER BY Barcode
    ";

    cmd.Parameters.AddWithValue("@t", "%" + text + "%");

    using var r = cmd.ExecuteReader();

    var list = new List<string>();

    while (r.Read())
        list.Add(r.GetString(0));

    return list;
}
public static bool Update(string barcode, int itemId, decimal unitsPerScan, out string message)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        UPDATE ItemBarcodes
        SET ItemId = @i,
            UnitsPerScan = @u
        WHERE Barcode = @b
    ";

    cmd.Parameters.AddWithValue("@b", barcode);
    cmd.Parameters.AddWithValue("@i", itemId);
    cmd.Parameters.AddWithValue("@u", unitsPerScan);

    int rows = cmd.ExecuteNonQuery();

    message = rows > 0 ? "✔ Barcode updated" : "❌ Barcode not found";
    return rows > 0;
}
public static int? GetItemIdByBarcode(string barcode)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT TOP 1 ItemId
        FROM ItemBarcodes
        WHERE Barcode = @barcode
    ";

    cmd.Parameters.AddWithValue("@barcode", barcode);

    var result = cmd.ExecuteScalar();

    if (result == null)
        return null;

    return Convert.ToInt32(result);
}
public static BarcodeItemResult? GetItemByBarcode(string barcode)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    using var cmd = conn.CreateCommand();

    cmd.CommandText = @"
        SELECT
            i.Id,
            i.Name,
            i.Alias,
            i.MarkedPrice,
            i.SellingPrice,
            i.WholesalePrice,
            i.Quantity,
            i.UnitType,
            i.UnitValue,
            ib.UnitsPerScan
        FROM ItemBarcodes ib
        INNER JOIN Items i
            ON i.Id = ib.ItemId
        WHERE
            ib.Barcode = @barcode
            AND i.BranchId = @branchId;
    ";

    cmd.Parameters.AddWithValue("@barcode", barcode);
    cmd.Parameters.AddWithValue("@branchId",
        SessionContext.EffectiveBranchId);

    using var reader = cmd.ExecuteReader();

    if (!reader.Read())
        return null;

    return new BarcodeItemResult
    {
        Item = new ItemModel
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Alias = reader.IsDBNull(2) ? "" : reader.GetString(2),
            MarkedPrice = reader.GetDecimal(3),
            SellingPrice = reader.GetDecimal(4),
            WholesalePrice = reader.IsDBNull(5)
                ? null
                : reader.GetDecimal(5),
            Quantity = reader.GetDecimal(6),
            UnitType = reader.GetString(7),
            UnitValue = reader.IsDBNull(8)
                ? null
                : reader.GetDecimal(8)
        },

        UnitsPerScan = reader.GetDecimal(9)
    };
}


    }

    public class ItemBarcode
    {
        public int ItemId { get; set; }
        public decimal UnitsPerScan { get; set; }
    }

        public class BarcodeItemResult
    {
        public ItemModel Item { get; set; } = null!;
        public decimal UnitsPerScan { get; set; }
    }
}

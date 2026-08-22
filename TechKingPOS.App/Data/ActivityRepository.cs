using System;
using System.Data;
using Microsoft.Data;
using Microsoft.Data.SqlClient;

using TechKingPOS.App.Models;

namespace TechKingPOS.App.Data
{
    public static class ActivityRepository
    {
public static void Log(Activity activity)
{
    using var connection = DbService.GetConnection();
    connection.Open();

    using var cmd = connection.CreateCommand();
    cmd.CommandText = @"
        INSERT INTO Activity (
            EntityType,
            EntityId,
            EntityName,
            Action,
            QuantityChange,
            UnitType,
            UnitValue,
            Price,
            BeforeValue,
            AfterValue,
            Reason,
            PerformedBy,
            BranchId,
            CreatedAt
        )
        VALUES (
            @EntityType,
            @EntityId,
            @EntityName,
            @Action,
            @QuantityChange,
            @UnitType,
            @UnitValue,
            @Price,
            @BeforeValue,
            @AfterValue,
            @Reason,
            @PerformedBy,
            @BranchId,
            @CreatedAt
        );
    ";

    cmd.Parameters.AddWithValue("@EntityType", activity.EntityType);
    cmd.Parameters.AddWithValue("@EntityId", (object?)activity.EntityId ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@EntityName",(object?)activity.EntityName ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@Action", activity.Action);
    cmd.Parameters.AddWithValue("@QuantityChange", activity.QuantityChange);
    cmd.Parameters.AddWithValue("@UnitType", (object?)activity.UnitType ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@UnitValue", (object?)activity.UnitValue ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@Price", (object?)activity.Price ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@BeforeValue", (object?)activity.BeforeValue ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@AfterValue", (object?)activity.AfterValue ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@Reason", (object?)activity.Reason ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@PerformedBy", activity.PerformedBy);
    cmd.Parameters.AddWithValue("@BranchId", activity.BranchId);
    cmd.Parameters.Add("@CreatedAt", SqlDbType.DateTime)
    .Value = activity.CreatedAt;


    cmd.ExecuteNonQuery();
}
public static void Log(
    SqlConnection connection,
    SqlTransaction transaction,
    Activity activity)
{
    using var cmd = connection.CreateCommand();
    cmd.Transaction = transaction;

    cmd.CommandText = @"
        INSERT INTO Activity (
            EntityType,
            EntityId,
            EntityName,
            Action,
            QuantityChange,
            UnitType,
            UnitValue,
            Price,
            BeforeValue,
            AfterValue,
            Reason,
            PerformedBy,
            BranchId,
            CreatedAt
        )
        VALUES (
            @EntityType,
            @EntityId,
            @EntityName,
            @Action,
            @QuantityChange,
            @UnitType,
            @UnitValue,
            @Price,
            @BeforeValue,
            @AfterValue,
            @Reason,
            @PerformedBy,
            @BranchId,
            @CreatedAt
        );
    ";

    cmd.Parameters.AddWithValue("@EntityType", activity.EntityType);
    cmd.Parameters.AddWithValue("@EntityId", (object?)activity.EntityId ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@EntityName",(object?)activity.EntityName ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@Action", activity.Action);
    cmd.Parameters.AddWithValue("@QuantityChange", activity.QuantityChange);
    cmd.Parameters.AddWithValue("@UnitType", (object?)activity.UnitType ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@UnitValue", (object?)activity.UnitValue ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@Price", (object?)activity.Price ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@BeforeValue", (object?)activity.BeforeValue ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@AfterValue", (object?)activity.AfterValue ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@Reason", (object?)activity.Reason ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@PerformedBy", activity.PerformedBy);
    cmd.Parameters.AddWithValue("@BranchId", activity.BranchId);
    cmd.Parameters.Add("@CreatedAt", SqlDbType.DateTime)
    .Value = activity.CreatedAt;


    cmd.ExecuteNonQuery();
}

public static List<Activity> GetLogs(DateTime from, DateTime to, int branchId)
{
    using var conn = DbService.GetConnection();
    conn.Open();

    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT *
        FROM Activity
        WHERE BranchId = @branch
        AND CreatedAt BETWEEN @from AND @to
        AND Action IN (
            'ADD_ITEM',
            'ITEM_SOLD',
            'DAMAGE',
            'REPACK_OUT',
            'REPACK_IN',
            'CUSTOMER_RETURN',
            'CUSTOMER_RETURN_RESTOCK',
            'SUPPLIER_RETURN',
            'STOCK_IN',
            'STOCK_OUT',
            'DELETE_ITEM',
            'DELETE_DAMAGE',
            'STOCK_ADJUSTED'
        )
        ORDER BY CreatedAt DESC
    ";

    cmd.Parameters.AddWithValue("@branch", branchId);
    cmd.Parameters.AddWithValue("@from", from);
    cmd.Parameters.AddWithValue("@to", to);

    using var reader = cmd.ExecuteReader();

    var list = new List<Activity>();

    while (reader.Read())
    {
        list.Add(Map(reader));
    }

    return list;
}
private static Activity Map(SqlDataReader reader)
{
    return new Activity
    {
        Id = reader.GetInt32(reader.GetOrdinal("Id")),

        EntityType = reader["EntityType"]?.ToString() ?? "",
        EntityId = reader["EntityId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["EntityId"]),
        EntityName = reader["EntityName"]?.ToString() ?? "",

        Action = reader["Action"]?.ToString() ?? "",

        QuantityChange = reader["QuantityChange"] == DBNull.Value
            ? 0
            : Convert.ToDecimal(reader["QuantityChange"]),

        UnitType = reader["UnitType"]?.ToString() ?? "",

        UnitValue = reader["UnitValue"] == DBNull.Value
            ? (decimal?)null
            : Convert.ToDecimal(reader["UnitValue"]),

        BeforeValue = reader["BeforeValue"]?.ToString(),
        AfterValue = reader["AfterValue"]?.ToString(),

        Reason = reader["Reason"]?.ToString(),
        PerformedBy = reader["PerformedBy"]?.ToString(),

        BranchId = reader["BranchId"] == DBNull.Value
            ? 0
            : Convert.ToInt32(reader["BranchId"]),

        CreatedAt = reader["CreatedAt"] == DBNull.Value
            ? DateTime.MinValue
            : Convert.ToDateTime(reader["CreatedAt"]),

        Price = reader["Price"] == DBNull.Value
            ? (decimal?)null
            : Convert.ToDecimal(reader["Price"])
    };
}
    }
}

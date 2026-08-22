using TechKingPOS.App.Models;

using Microsoft.Data.SqlClient;

namespace TechKingPOS.App.Data
{
    public static class SyncQueueRepository
    {
public static void Add(
    string table,
    int recordId,
    string action,
    string json,
    int priority = 1)
{
    var cloud = CloudSettingsRepository.Get();

    if (cloud == null)
        return;

    using SqlConnection con = DbService.GetConnection();

    con.Open();

    using SqlCommand cmd = new(@"
INSERT INTO SyncQueue
(
BusinessCode,
BranchId,
DeviceId,

TableName,
RecordId,
Action,
JsonData,

Priority
)
VALUES
(
@BusinessCode,
@BranchId,
@DeviceId,

@Table,
@RecordId,
@Action,
@Json,

@Priority
)", con);

    cmd.Parameters.AddWithValue("@BusinessCode", cloud.BusinessCode);
    cmd.Parameters.AddWithValue("@BranchId", cloud.BranchId);
    cmd.Parameters.AddWithValue("@DeviceId", cloud.DeviceId);

    cmd.Parameters.AddWithValue("@Table", table);
    cmd.Parameters.AddWithValue("@RecordId", recordId);
    cmd.Parameters.AddWithValue("@Action", action);
    cmd.Parameters.AddWithValue("@Json", json);

    cmd.Parameters.AddWithValue("@Priority", priority);

    cmd.ExecuteNonQuery();
}
public static List<SyncQueueModel> GetPending()
{
    List<SyncQueueModel> list = new();

    using SqlConnection con = DbService.GetConnection();

    con.Open();

    using SqlCommand cmd = new(@"
SELECT Id,BusinessCode,BranchId,DeviceId,
TableName,RecordId,Action,JsonData,
Priority,Status,Attempts,LastError,CreatedAt,
SyncedAt,NextRetryAt
FROM SyncQueue
WHERE Status='Pending'
AND
(
    NextRetryAt IS NULL
    OR
    NextRetryAt<=SYSDATETIME()
)
ORDER BY Priority DESC,CreatedAt;
", con);

    using SqlDataReader r = cmd.ExecuteReader();

    while (r.Read())
    {
        list.Add(new SyncQueueModel
        {
            Id = Convert.ToInt32(r["Id"]),

            BusinessCode = r["BusinessCode"].ToString()!,

            BranchId = Convert.ToInt32(r["BranchId"]),

            DeviceId = r["DeviceId"].ToString()!,

            TableName = r["TableName"].ToString()!,

            RecordId = Convert.ToInt32(r["RecordId"]),

            Action = r["Action"].ToString()!,

            JsonData = r["JsonData"].ToString()!,

            Priority = Convert.ToInt32(r["Priority"]),

            Status = r["Status"].ToString()!,

            Attempts = Convert.ToInt32(r["Attempts"]),

            LastError = r["LastError"] == DBNull.Value
                ? ""
                : r["LastError"].ToString()!,

            CreatedAt = Convert.ToDateTime(r["CreatedAt"]),

            SyncedAt = r["SyncedAt"] == DBNull.Value
                ? null
                : Convert.ToDateTime(r["SyncedAt"]),

            NextRetryAt = r["NextRetryAt"] == DBNull.Value
                ? null
                : Convert.ToDateTime(r["NextRetryAt"])
        });
    }

    return list;
}
        public static void MarkSynced(int id)
        {
            using SqlConnection con = DbService.GetConnection();

            con.Open();

            using SqlCommand cmd = new(@"
UPDATE SyncQueue
SET
Status='Synced',
SyncedAt=SYSDATETIME()
WHERE Id=@Id
", con);

            cmd.Parameters.AddWithValue("@Id", id);

            cmd.ExecuteNonQuery();
        }

public static void MarkFailed(
    int id,
    string error)
{
    using SqlConnection con = DbService.GetConnection();

    con.Open();

    using SqlCommand cmd = new(@"
UPDATE SyncQueue
SET

Attempts=Attempts+1,

Status='Pending',

LastError=@Error,

NextRetryAt=DATEADD(MINUTE,1,SYSDATETIME())

WHERE Id=@Id
", con);

    cmd.Parameters.AddWithValue("@Id", id);
    cmd.Parameters.AddWithValue("@Error", error);

    cmd.ExecuteNonQuery();
}
    }
}
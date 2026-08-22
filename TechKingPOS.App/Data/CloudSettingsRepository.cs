using Microsoft.Data.SqlClient;
using TechKingPOS.App.Models;
//using System.Net;


namespace TechKingPOS.App.Data
{
public static class CloudSettingsRepository
{

public static void Save(
    string businessCode,
    int businessId,
    int branchId)
{
    using SqlConnection con = DbService.GetConnection();

    con.Open();

    string sql =
    @"
IF EXISTS(SELECT TOP 1 Id FROM CloudSettings)
BEGIN

    UPDATE CloudSettings
    SET

        BusinessCode=@BusinessCode,
        BusinessId=@BusinessId,
        BranchId=@BranchId,

        DeviceId=@DeviceId,
        DeviceName=@DeviceName,

        ApiUrl=@ApiUrl,

        SyncEnabled=1,

        UpdatedAt=SYSDATETIME()

END
ELSE
BEGIN

    INSERT INTO CloudSettings
    (

        BusinessCode,
        BusinessId,
        BranchId,

        DeviceId,
        DeviceName,

        ApiUrl,

        SyncEnabled

    )

    VALUES
    (

        @BusinessCode,
        @BusinessId,
        @BranchId,

        @DeviceId,
        @DeviceName,

        @ApiUrl,

        1

    )

END
";

    using SqlCommand cmd = new(sql, con);

    cmd.Parameters.AddWithValue("@BusinessCode", businessCode);
    cmd.Parameters.AddWithValue("@BusinessId", businessId);
    cmd.Parameters.AddWithValue("@BranchId", branchId);

    cmd.Parameters.AddWithValue("@DeviceId", Guid.NewGuid().ToString());

    cmd.Parameters.AddWithValue(
        "@DeviceName",
        Environment.MachineName);

    cmd.Parameters.AddWithValue(
        "@ApiUrl",
        ApiRoutes.BaseUrl);

    cmd.ExecuteNonQuery();
}
public static CloudSettingsModel? Get()
{
    using SqlConnection con = DbService.GetConnection();

    con.Open();

    using SqlCommand cmd = new(
    @"
SELECT TOP 1 *
FROM CloudSettings
",
    con);

    using SqlDataReader r = cmd.ExecuteReader();

    if (!r.Read())
        return null;

    return new CloudSettingsModel
    {
        Id = Convert.ToInt32(r["Id"]),

        BusinessCode = r["BusinessCode"].ToString()!,

        BusinessId = Convert.ToInt32(r["BusinessId"]),

        BranchId = Convert.ToInt32(r["BranchId"]),

        DeviceId = r["DeviceId"].ToString()!,

        DeviceName = r["DeviceName"].ToString()!,

        ApiUrl = r["ApiUrl"].ToString()!,

        ApiKey = r["ApiKey"] == DBNull.Value
            ? null
            : r["ApiKey"].ToString(),

        SyncEnabled = Convert.ToBoolean(r["SyncEnabled"]),

        LastUpload = r["LastUpload"] == DBNull.Value
            ? null
            : Convert.ToDateTime(r["LastUpload"]),

        LastDownload = r["LastDownload"] == DBNull.Value
            ? null
            : Convert.ToDateTime(r["LastDownload"]),

        SyncIntervalSeconds =
            Convert.ToInt32(r["SyncIntervalSeconds"]),

        CreatedAt =
            Convert.ToDateTime(r["CreatedAt"]),

        UpdatedAt =
            r["UpdatedAt"] == DBNull.Value
            ? null
            : Convert.ToDateTime(r["UpdatedAt"])
    };
}
public static void UpdateLastSync()
{
    using SqlConnection con = DbService.GetConnection();

    con.Open();

    using SqlCommand cmd = new(
    @"
UPDATE CloudSettings
SET

LastUpload=@Date,
UpdatedAt=SYSDATETIME()
",
    con);

    cmd.Parameters.AddWithValue(
        "@Date",
        DateTime.Now);

    cmd.ExecuteNonQuery();
}
public static void UpdateLastUpload()
{
    using SqlConnection con = DbService.GetConnection();

    con.Open();

    using SqlCommand cmd = new(
    @"
UPDATE CloudSettings
SET

LastUpload=@Date,
UpdatedAt=SYSDATETIME()
",
    con);

    cmd.Parameters.AddWithValue(
        "@Date",
        DateTime.Now);

    cmd.ExecuteNonQuery();
}
public static void UpdateLastDownload()
{
    using SqlConnection con = DbService.GetConnection();

    con.Open();

    using SqlCommand cmd = new(
    @"
UPDATE CloudSettings
SET

LastDownload=@Date,
UpdatedAt=SYSDATETIME()
",
    con);

    cmd.Parameters.AddWithValue(
        "@Date",
        DateTime.Now);

    cmd.ExecuteNonQuery();
}
public static void SetSyncEnabled(bool enabled)
{
    using SqlConnection con = DbService.GetConnection();

    con.Open();

    using SqlCommand cmd = new(
    @"
UPDATE CloudSettings
SET

SyncEnabled=@Enabled,
UpdatedAt=SYSDATETIME()
",
    con);

    cmd.Parameters.AddWithValue(
        "@Enabled",
        enabled);

    cmd.ExecuteNonQuery();
}
}
}
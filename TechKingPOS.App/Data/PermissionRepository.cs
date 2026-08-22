using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using TechKingPOS.App.Models;
using TechKingPOS.App.Services;
using TechKingPOS.App.Security;

namespace TechKingPOS.App.Data
{
    public static class PermissionRepository
    {
        // ================= SEED =================
        public static void SeedPermissions(IEnumerable<string> keys)
        {
            using var conn = DbService.GetConnection();
            conn.Open();

            foreach (var key in keys)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    IF NOT EXISTS (SELECT 1 FROM Permissions WHERE UserKey = @key)
                    BEGIN
                        INSERT INTO Permissions (UserKey) VALUES (@key);
                    END
                ";
                cmd.Parameters.AddWithValue("@key", key);
                cmd.ExecuteNonQuery();
            }
        }

        // ================= CHECK =================
        public static bool HasPermission(int userId, string permissionKey)
        {
            using var conn = DbService.GetConnection();
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                        SELECT TOP 1 granted
                        FROM user_permissions
                        WHERE user_id = @uid
                        AND permission_key = @key;
                    ";

            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@key", permissionKey);

            var result = cmd.ExecuteScalar();
            return result != null && Convert.ToInt32(result) == 1;
        }

        // ================= SET / UPDATE =================
        public static void SetUserPermission(
            int userId,
            string permissionKey,
            bool granted
        )
        {
            using var conn = DbService.GetConnection();
            conn.Open();

            using var transaction = conn.BeginTransaction();

            try
            {
                // 🔎 Check previous state
                bool? previous = null;

                var checkCmd = conn.CreateCommand();
                checkCmd.Transaction = transaction;
                checkCmd.CommandText = @"
                        SELECT granted
                        FROM user_permissions
                        WHERE user_id = @uid
                        AND permission_key = @key;
                    ";
                checkCmd.Parameters.AddWithValue("@uid", userId);
                checkCmd.Parameters.AddWithValue("@key", permissionKey);

                var prevResult = checkCmd.ExecuteScalar();
                if (prevResult != null)
                    previous = Convert.ToInt32(prevResult) == 1;

                // 💾 UPSERT using MERGE
                var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    MERGE user_permissions AS target
                    USING (SELECT @uid AS user_id, @key AS permission_key) AS source
                    ON target.user_id = source.user_id 
                    AND target.permission_key = source.permission_key
                    WHEN MATCHED THEN 
                        UPDATE SET granted = @granted, granted_at = @at
                    WHEN NOT MATCHED THEN
                        INSERT (user_id, permission_key, granted, granted_at)
                        VALUES (@uid, @key, @granted, @at);
                ";
                cmd.Parameters.AddWithValue("@uid", userId);
                cmd.Parameters.AddWithValue("@key", permissionKey);
                cmd.Parameters.AddWithValue("@granted", granted ? 1 : 0);
                cmd.Parameters.AddWithValue("@at", DateTime.Now);

                cmd.ExecuteNonQuery();

                // 📝 ACTIVITY LOG
                ActivityRepository.Log(conn, transaction, new Activity
                {
                    EntityType = "USER_PERMISSION",
                    EntityId = userId,
                    EntityName = permissionKey,
                    Action = granted ? "PERMISSION_GRANTED" : "PERMISSION_REVOKED",
                    BeforeValue = previous.HasValue
                        ? (previous.Value ? "Granted" : "Denied")
                        : null,
                    AfterValue = granted ? "Granted" : "Denied",
                    Reason = $"Permission {(granted ? "granted" : "revoked")}",
                    PerformedBy = SessionContext.CurrentUserName,
                    BranchId = SessionContext.EffectiveBranchId,
                    CreatedAt = DateTime.Now
                });

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}

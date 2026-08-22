using System;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TechKingPOS.App.Models;
using TechKingPOS.App.Data;
using TechKingPOS.App.Infrastructure;
using TechKingPOS.App.Services;
using TechKingPOS.App.Security;

namespace TechKingPOS.App.Data
{

    public static class BranchRepository
    {    

        // ================= GET ALL =================
        public static List<Branch> GetAll()
        {
            var list = new List<Branch>();

            using var conn = DbService.GetConnection();
            conn.Open();

            var cmd = conn.CreateCommand();
            // Admins can see all branches, non-admins see only their own branch
            if (SessionContext.IsAdmin)
            {
                cmd.CommandText = @"
                    SELECT Id, Name, Code, IsActive
                    FROM Branch
                    ORDER BY Name;
                ";
            }
            else
            {
                cmd.CommandText = @"
                    SELECT Id, Name, Code, IsActive
                    FROM Branch
                    WHERE Id = @branchId
                    ORDER BY Name;
                ";
                cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);
            }

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Branch
                {
                    Id = r.GetInt32(0),
                    Name = r.GetString(1),
                    Code = r.IsDBNull(2) ? "" : r.GetString(2),
                    IsActive = r.GetBoolean(3)
                });
            }

            return list;
        }

        // ================= INSERT =================
        public static void Insert(string name, string code, bool isActive)
        {
            using var conn = DbService.GetConnection();
            conn.Open();

            using var tx = conn.BeginTransaction();

            try
            {
                var createdAt = DateTime.Now;

                var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO Branch (Name, Code, IsActive, CreatedAt)
                    Output INSERTED.Id
                    VALUES (@name, @code, @active, @created);
                    SELECT SCOPE_IDENTITY();
                ";

                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@code",
                    string.IsNullOrWhiteSpace(code) ? DBNull.Value : code);
                cmd.Parameters.AddWithValue("@active", isActive );
                cmd.Parameters.AddWithValue("@created",
                    createdAt);

                
                int branchId = Convert.ToInt32(cmd.ExecuteScalar());

                // 🔥 ACTIVITY LOG
                ActivityRepository.Log(conn, tx, new Activity
                {
                    EntityType = "Branch",
                    EntityId = (int)branchId,
                    EntityName = name,
                    Action = "BRANCH_CREATED",
                    AfterValue = $"Name={name}, Code={code}, Active={(isActive ? 1 : 0)}",
                    PerformedBy = SessionContext.CurrentUserName,
                    BranchId = (int)branchId,
                    CreatedAt = createdAt
                });
                var branch = new
                {
                    Id = branchId,
                    Name = name,
                    Code = code,
                    IsActive = isActive
                };


                SyncQueueRepository.Add(
                    "Branch",
                    branchId,
                    "INSERT",
                    JsonSerializer.Serialize(branch)
                );
            

                tx.Commit();
                AppState.NotifyDataChanged();

            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // ================= UPDATE =================
        public static void Update(int id, string name, string code, bool isActive)
        {
            using var conn = DbService.GetConnection();
            conn.Open();

            using var tx = conn.BeginTransaction();

            try
            {
                // read BEFORE values (for audit)
                var beforeCmd = conn.CreateCommand();
                beforeCmd.Transaction = tx;
                beforeCmd.CommandText = @"
                    SELECT Name, Code, IsActive
                    FROM Branch
                    WHERE Id = @id;
                ";
                beforeCmd.Parameters.AddWithValue("@id", id);

                string before = "";
                using (var r = beforeCmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        before =
                            $"Name={r.GetString(0)}, Code={(r.IsDBNull(1) ? "" : r.GetString(1))}, Active={r.GetBoolean(2)}"; }
                }

                var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    UPDATE Branch
                    SET Name = @name,
                        Code = @code,
                        IsActive = @active
                    WHERE Id = @id;
                ";

                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@code",
                    string.IsNullOrWhiteSpace(code) ? DBNull.Value : code);
                cmd.Parameters.AddWithValue("@active", isActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@id", id);

                cmd.ExecuteNonQuery();

                // 🔥 ACTIVITY LOG
                ActivityRepository.Log(conn, tx, new Activity
                {
                    EntityType = "Branch",
                    EntityId = id,
                    EntityName = name,
                    Action = "BRANCH_UPDATED",
                    BeforeValue = before,
                    AfterValue = $"Name={name}, Code={code}, Active={(isActive ? 1 : 0)}",
                    PerformedBy = SessionContext.CurrentUserName,
                    BranchId = id,
                    CreatedAt = DateTime.Now
                });
                var branch = new
                {
                    Id = id,
                    Name = name,
                    Code = code,
                    IsActive = isActive
                };
                SyncQueueRepository.Add(
                    "Branch",
                    id,
                    "UPDATE",
                    JsonSerializer.Serialize(branch)
                );

                tx.Commit();
                AppState.NotifyDataChanged();

            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // ================= GET DEFAULT =================
        public static Branch GetDefault()
        {
            using var conn = DbService.GetConnection();
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT TOP (1) Id, Name, Code, IsActive
                FROM Branch
                WHERE IsActive = 1
                ORDER BY Id
                ;
            ";

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            return new Branch
            {
                Id = r.GetInt32(0),
                Name = r.GetString(1),
                Code = r.IsDBNull(2) ? "" : r.GetString(2),
                IsActive = r.GetBoolean(3)
            };
        }

        // ================= GET ACTIVE =================
        public static List<Branch> GetActive()
        {
            var list = new List<Branch>();

            using var conn = DbService.GetConnection();
            conn.Open();

            var cmd = conn.CreateCommand();
            // Admins can see all active branches, non-admins see only their branch
            if (SessionContext.IsAdmin)
            {
                cmd.CommandText = @"
                    SELECT Id, Name, Code, IsActive
                    FROM Branch
                    WHERE IsActive = 1
                    ORDER BY Name;
                ";
            }
            else
            {
                cmd.CommandText = @"
                    SELECT Id, Name, Code, IsActive
                    FROM Branch
                    WHERE IsActive = 1 AND Id = @branchId
                    ORDER BY Name;
                ";
                cmd.Parameters.AddWithValue("@branchId", SessionContext.EffectiveBranchId);
            }

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new Branch
                {
                    Id = r.GetInt32(0),
                    Name = r.GetString(1),
                    Code = r.GetString(2),
                    IsActive = r.GetBoolean(3)
                });
            }

            return list;
        }

        // ================= GET BY ID =================
        public static Branch GetById(int id)
        {
            using var conn = DbService.GetConnection();
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT TOP (1)Id, Name, Code, IsActive
                FROM Branch
                WHERE Id = @id
                ;
            ";
            cmd.Parameters.AddWithValue("@id", id);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            return new Branch
            {
                Id = r.GetInt32(0),
                Name = r.GetString(1),
                Code = r.IsDBNull(2) ? "" : r.GetString(2),
                IsActive = r.GetBoolean(3)
            };
        }
    }
}

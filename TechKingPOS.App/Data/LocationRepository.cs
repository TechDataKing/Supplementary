using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using Microsoft.Data.SqlClient;
using TechKingPOS.App.Models;
using TechKingPOS.App.Services;
using TechKingPOS.App.Security;

namespace TechKingPOS.App.Data
{
    public class LocationRepository
    {
        private readonly SqlConnection _connection;

        public LocationRepository(SqlConnection connection)
        {
            _connection = connection;
        }

        // ================= GET ROOTS =================
        public List<LocationModel> GetRoots()
        {
            var list = new List<LocationModel>();

            try
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT * FROM Locations WHERE ParentId IS NULL";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(Map(reader));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fetching root locations:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return list;
        }

        // ================= GET CHILDREN =================
        public List<LocationModel> GetChildren(int parentId)
        {
            var list = new List<LocationModel>();

            try
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT * FROM Locations WHERE ParentId=@parent";
                cmd.Parameters.AddWithValue("@parent", parentId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(Map(reader));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fetching child locations:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return list;
        }

        // ================= ADD =================
        public int Add(LocationModel model)
        {
            try
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Locations (Name, ParentId, Type)
                    OUTPUT INSERTED.Id
                    VALUES (@name, @parent, @type)";
                cmd.Parameters.AddWithValue("@name", model.Name);
                cmd.Parameters.AddWithValue("@parent",
                    model.ParentId.HasValue ? (object)model.ParentId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@type", model.Type);

                int id = (int)cmd.ExecuteScalar();

                // Activity Log
                ActivityRepository.Log(new Activity
                {
                    EntityType = "Location",
                    EntityId = id,
                    EntityName = model.Name,
                    Action = "LOCATION_CREATED",
                    AfterValue = $"Name={model.Name}, Type={model.Type}, ParentId={model.ParentId}",
                    PerformedBy = SessionContext.CurrentUserName,
                    BranchId = SessionContext.CurrentBranchId,
                    CreatedAt = DateTime.Now
                });

                return id;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding location:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return -1;
            }
        }

        // ================= UPDATE =================
        public void Update(LocationModel model)
        {
            try
            {
                // Get current values for logging
                var before = "";
                using (var cmdBefore = _connection.CreateCommand())
                {
                    cmdBefore.CommandText = "SELECT Name, Type, ParentId FROM Locations WHERE Id=@id";
                    cmdBefore.Parameters.AddWithValue("@id", model.Id);
                    using var reader = cmdBefore.ExecuteReader();
                    if (reader.Read())
                    {
                        before = $"Name={reader["Name"]}, Type={reader["Type"]}, ParentId={reader["ParentId"]}";
                    }
                }

                using var cmd = _connection.CreateCommand();
                cmd.CommandText = @"
                    UPDATE Locations
                    SET Name=@name, Type=@type, UpdatedAt=SYSDATETIME()
                    WHERE Id=@id";
                cmd.Parameters.AddWithValue("@name", model.Name);
                cmd.Parameters.AddWithValue("@type", model.Type);
                cmd.Parameters.AddWithValue("@id", model.Id);

                cmd.ExecuteNonQuery();

                // Activity Log
                ActivityRepository.Log(new Activity
                {
                    EntityType = "Location",
                    EntityId = model.Id,
                    EntityName = model.Name,
                    Action = "LOCATION_UPDATED",
                    BeforeValue = before,
                    AfterValue = $"Name={model.Name}, Type={model.Type}, ParentId={model.ParentId}",
                    PerformedBy = SessionContext.CurrentUserName,
                    BranchId = SessionContext.CurrentBranchId,
                    CreatedAt = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating location:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================= DELETE =================
        public void Delete(int id)
        {
            try
            {
                // Get current values for logging
                var before = "";
                using (var cmdBefore = _connection.CreateCommand())
                {
                    cmdBefore.CommandText = "SELECT Name, Type, ParentId FROM Locations WHERE Id=@id";
                    cmdBefore.Parameters.AddWithValue("@id", id);
                    using var reader = cmdBefore.ExecuteReader();
                    if (reader.Read())
                    {
                        before = $"Name={reader["Name"]}, Type={reader["Type"]}, ParentId={reader["ParentId"]}";
                    }
                }

                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "DELETE FROM Locations WHERE Id=@id";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();

                // Activity Log
                ActivityRepository.Log(new Activity
                {
                    EntityType = "Location",
                    EntityId = id,
                    Action = "LOCATION_DELETED",
                    BeforeValue = before,
                    PerformedBy = SessionContext.CurrentUserName,
                    BranchId = SessionContext.CurrentBranchId,
                    CreatedAt = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting location:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================= MAP =================
        private LocationModel Map(SqlDataReader reader)
        {
            try
            {
                return new LocationModel
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    Name = reader["Name"].ToString() ?? "",
                    ParentId = reader["ParentId"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(reader["ParentId"]),
                    Type = reader["Type"].ToString() ?? "",
                    CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                    UpdatedAt = reader["UpdatedAt"] == DBNull.Value
                        ? null
                        : Convert.ToDateTime(reader["UpdatedAt"])
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error mapping location:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
        // ================= GET LOCATION ID BY NAME =================
public int GetIdByName(string name, SqlTransaction? tx = null)
{
    try
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT TOP 1 Id 
            FROM Locations 
            WHERE Name = @name
        ";
        cmd.Parameters.AddWithValue("@name", name);

        if (tx != null)
            cmd.Transaction = tx; // assign transaction if provided

        object? result = cmd.ExecuteScalar();
        if (result == null || result == DBNull.Value)
            throw new InvalidOperationException($"Location '{name}' not found.");

        return Convert.ToInt32(result);
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error fetching location ID:\n{ex.Message}", "Location Error", MessageBoxButton.OK, MessageBoxImage.Error);
        return -1;
    }
}
    }
}

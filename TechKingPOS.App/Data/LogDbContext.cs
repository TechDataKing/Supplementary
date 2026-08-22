using Microsoft.Data.SqlClient;
using System;
using System.IO;

namespace TechKingPOS.App.Data
{
    public static class LogDb
    {
        private static readonly string DbPath =
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "logs",
                "logs.db"
            );

        public static SqlConnection GetConnection()
        {
            var dir = Path.GetDirectoryName(DbPath);

            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            return new SqlConnection($"Data Source={DbPath}");
        }

        public static void Initialize()
        {
            using var conn = GetConnection();
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText =
            @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'logs')
            BEGIN   
                CREATE TABLE logs (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Timestamp DATETIME2,
                    Level NVARCHAR(20),
                    Emoji NVARCHAR(10),
                    Category NVARCHAR(100),
                    Message NVARCHAR(MAX),
                    Details NVARCHAR(MAX)
                );
            END
            ";
            cmd.ExecuteNonQuery();
        }
    }
}

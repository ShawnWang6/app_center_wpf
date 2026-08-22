using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CtrlCenter.Storage
{
    public class DabInitializer
    {
        private readonly string _connectionString;

        public DabInitializer(string connString)
        {
            // 连接字符串
            _connectionString = connString;
        }

        public void EnsureDatabaseCreated()
        {
            // 1. 确保目录存在
            var directory = Path.GetDirectoryName(_connectionString.Replace("Data Source=", ""));
            directory = directory.TrimEnd(';');
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 2. 检查数据库是否已存在
            var dbPath = _connectionString.Replace("Data Source=", "");
            dbPath = dbPath.TrimEnd(';');
            if (File.Exists(dbPath))
            {
                // 可选：检查表是否存在
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SwitchHisEntity';";
                using var reader = command.ExecuteReader();
                if (reader.Read() && reader.GetInt32(0) >= 0)
                {
                    return; // 数据库已存在且包含表，跳过初始化
                }
            }

            // 3. 执行初始化脚本
            ExecuteScripts();
        }

        private void ExecuteScripts()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // 使用事务确保脚本原子性
            using var transaction = connection.BeginTransaction();

            try
            {
                // 执行 SQL 脚本
                var scripts = GetInitializationScripts();
                using var command = connection.CreateCommand();
                foreach (var script in scripts)
                {
                    command.CommandText = script;
                    command.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private IEnumerable<string> GetInitializationScripts()
        {
            return new[]
            {
            // 用户表
            @"
    CREATE TABLE IF NOT EXISTS SwitchHisEntity (
    Id INTEGER PRIMARY KEY AUTOINCREMENT, -- Auto-incrementing ID
    SwitchNo TEXT NOT NULL,               -- Switch number
    MinTime DATETIME NOT NULL,            -- Earliest experiment time
    MaxTime DATETIME NOT NULL,            -- Latest experiment time
    CreateTime DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, -- Creation time
    RptExcel BLOB,
    RptJson TEXT                          -- JSON serialized report information
    );",

            // 创建索引
            @"
     CREATE INDEX IF NOT EXISTS idx_SwitchNo ON SwitchHisEntity (SwitchNo);",
            };
        }
    }
}

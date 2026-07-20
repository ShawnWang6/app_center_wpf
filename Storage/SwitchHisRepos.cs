using CtrlCenter.DataModel;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CtrlCenter.Storage
{
    public class SwitchHisRepos : ISwitchHisRepos
    {
        readonly IDbConnection Connection;
        public SwitchHisRepos(IDbConnection conn)
        {
            Connection = conn;
        }
        public string SaveSwitchHis(SwitchHisEntity entity)
        {
            string err = string.Empty;
            try
            {
                // Save to database
                using var command = Connection.CreateCommand();
                command.CommandText = @"
        INSERT INTO SwitchHisEntity (SwitchNo, MinTime, MaxTime, CreateTime, RptJson)
        VALUES (@SwitchNo, @MinTime, @MaxTime, @CreateTime, @RptJson);
        SELECT last_insert_rowid();";
                command.Parameters.Add(new SqliteParameter("@SwitchNo", entity.SwitchNo));
                command.Parameters.Add(new SqliteParameter("@MinTime", entity.MinTime));
                command.Parameters.Add(new SqliteParameter("@MaxTime", entity.MaxTime));
                //command.Parameters.Add(new SqliteParameter("@CreateTime", entity));
                command.Parameters.Add(new SqliteParameter("@RptJson", entity.RptJson));
                entity.Id = Convert.ToInt32(command.ExecuteScalar());
            }
            catch(Exception ex)
            {
                err = ex.Message;
            }
            return err;
        }
        public IEnumerable<SwitchHisEntity> GetSwitchHis(string switchNo, DateTime? startTime, DateTime? minTime)
        {
            // Build the SQL query
            var query = new StringBuilder("SELECT * FROM SwitchHisEntity WHERE 1=1");

            // Add conditions based on the provided parameters
            if (!string.IsNullOrEmpty(switchNo))
            {
                query.Append(" AND SwitchNo = @SwitchNo");
            }
            if (startTime.HasValue)
            {
                query.Append(" AND MaxTime >= @StartTime");
            }
            if (minTime.HasValue)
            {
                query.Append(" AND MinTime <= @MinTime");
            }

            // Prepare the command
            using var command = Connection.CreateCommand();
            command.CommandText = query.ToString();

            // Add parameters to the command
            if (!string.IsNullOrEmpty(switchNo))
            {
                var switchNoParam = command.CreateParameter();
                switchNoParam.ParameterName = "@SwitchNo";
                switchNoParam.Value = switchNo;
                command.Parameters.Add(switchNoParam);
            }
            if (startTime.HasValue)
            {
                var startTimeParam = command.CreateParameter();
                startTimeParam.ParameterName = "@StartTime";
                startTimeParam.Value = startTime.Value;
                command.Parameters.Add(startTimeParam);
            }
            if (minTime.HasValue)
            {
                var minTimeParam = command.CreateParameter();
                minTimeParam.ParameterName = "@MinTime";
                minTimeParam.Value = minTime.Value;
                command.Parameters.Add(minTimeParam);
            }

            // Execute the query and map the results to SwitchHisEntity objects
            var results = new List<SwitchHisEntity>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new SwitchHisEntity
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    SwitchNo = reader.GetString(reader.GetOrdinal("SwitchNo")),
                    MinTime = reader.GetDateTime(reader.GetOrdinal("MinTime")),
                    MaxTime = reader.GetDateTime(reader.GetOrdinal("MaxTime")),
                    CreateTime = reader.GetDateTime(reader.GetOrdinal("CreateTime")),
                    RptJson = reader.IsDBNull(reader.GetOrdinal("RptJson")) ? null : reader.GetString(reader.GetOrdinal("RptJson"))
                });
            }

            return results;
        }
    }
}

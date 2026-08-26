using CtrlCenter.DataModel;
using CtrlCenter.Interfaces;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CtrlCenter.Storage
{
    public class SwitchHisRepos : ISwitchHisRepos
    {
        readonly IDbConnFactory DbConnFactory;
        public SwitchHisRepos(IDbConnFactory dbConnFactory)
        {
            DbConnFactory = dbConnFactory;
        }

        public string SaveSwitchHis(SwitchHisEntity entity)
        {
            string err = string.Empty;
            try
            {           
                using var conn = DbConnFactory.CreateConnection();
                const string sql = @"
        INSERT INTO SwitchHisEntity (SwitchNo, MinTime, MaxTime, RptJson)
        VALUES (@SwitchNo, @MinTime, @MaxTime, @RptJson);
        SELECT last_insert_rowid();";            
                var id = conn.ExecuteScalar<long>(sql, new
                {
                    entity.SwitchNo,
                    entity.MinTime,
                    entity.MaxTime,
                    entity.RptJson
                });                
                entity.Id = id;
            }
            catch (Exception ex)
            {
                err = ex.Message;
            }
            return err;
        }
        public IList<SwitchHisEntity> GetSwitchHis(string switchNo, DateTime? startTime, DateTime? minTime)
        {
            // Build the SQL query
            var query = new StringBuilder($"SELECT {SwitchHisEntity.MainFields} FROM SwitchHisEntity WHERE 1=1");
            var parameters = new DynamicParameters();

            // Add conditions based on the provided parameters
            if (!string.IsNullOrEmpty(switchNo))
            {
                query.Append(" AND SwitchNo = @SwitchNo");
                parameters.Add("SwitchNo", switchNo);
            }
            if (startTime.HasValue)
            {
                query.Append(" AND MaxTime >= @StartTime");
                parameters.Add("StartTime", startTime.Value);
            }
            if (minTime.HasValue)
            {
                query.Append(" AND MinTime <= @MinTime");
                parameters.Add("MinTime", minTime.Value);
            }

            using var connection = DbConnFactory.CreateConnection();
            //var apps = connection.Query<App>(sql, new { Status = status, Version = version });
            return connection.Query<SwitchHisEntity>(query.ToString(), parameters).ToList();
        }

        byte[] ISwitchHisRepos.GetExcel(long hisId)
        {
            using var conn = DbConnFactory.CreateConnection();
            return conn.Query<byte[]>($"SELECT RptExcel FROM SwitchHisEntity WHERE Id={hisId}").FirstOrDefault();
        }

        string ISwitchHisRepos.SetExcel(long hisId, byte[] rptExcel)
        {
            string err = string.Empty;
            try
            {
                using var conn = DbConnFactory.CreateConnection();
                string sql = "UPDATE SwitchHisEntity SET RptExcel = @RptExcel WHERE id = @Id;";
                int affectedRows = conn.Execute(sql, new { RptExcel = rptExcel, Id = hisId });
            }
            catch (Exception ex)
            {
                err = ex.Message;
            }
            return err;
        }
    }
}

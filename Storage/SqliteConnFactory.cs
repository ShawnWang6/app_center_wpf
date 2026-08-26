using CtrlCenter.DataModel;
using CtrlCenter.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CtrlCenter.Storage
{
    public class SqliteConnFactory : IDbConnFactory
    {
        private readonly AppSetting _appSetting;

        public SqliteConnFactory(AppSetting appSetting)
        {
            _appSetting = appSetting;
        }

        public IDbConnection CreateConnection()
        {
            return new SqliteConnection(_appSetting.DbOptions.ConnString);
        }

        public IDbConnection CreateConnection(string connectionString)
        {
            return new SqliteConnection(connectionString);
        }
    }
}

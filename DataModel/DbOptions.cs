using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CtrlCenter.Storage
{
    public class DbOptions
    {
        // 属性名必须和 JSON 中的键名一致
        public string ConnString { get; set; } = $"Data Source={Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "report_his.db")};";
        public int CmdTimeout { get; set; }
    }
}

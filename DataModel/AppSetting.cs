using CtrlCenter.Storage;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CtrlCenter.DataModel
{
    public class AppSetting
    {
        public string Name { get; set; }

        /// <summary>
        //  RptHisManager从数据库加载历史最大间隔
        /// </summary>
        public TimeSpan LoadHisMaxTimeSpan { get; set; }

        public DbOptions DbOptions { get; set; } = new DbOptions();
        
        //[Range(1, 100, ErrorMessage = "重试次数必须在1-100之间")]
        //public int MaxRetryCount { get; set; }

        public bool EnableLogging { get; set; }

        //[Required]
        //public EmailSettings Email { get; set; }
    }
}

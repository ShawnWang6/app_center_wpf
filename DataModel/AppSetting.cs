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
        public TimeSpan LoadHisMaxTimeSpan { get; set; } = TimeSpan.FromDays(7);

        public DbOptions DbOptions { get; set; } = new DbOptions();
        
        //[Range(1, 100, ErrorMessage = "重试次数必须在1-100之间")]
        //public int MaxRetryCount { get; set; }

        public bool EnableLogging { get; set; }

        public bool TopMost { get; set; } = true;

        //[Required]
        //public EmailSettings Email { get; set; }

        /// <summary>
        //  仅描扫描改时间戳据当前时间最大的时间间隔(单位秒), 默认只扫描最近5分钟的报表文件
        /// </summary>
        public long ScanFileMaxTimeSpanSec { get; set; } = 300;
    }
}

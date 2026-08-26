using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CtrlCenter.Storage
{

    public class SwitchHisEntity
    {
        public const string Fields = "Id, SwitchNo,MinTime,MinTime,CreateTime,RptExcel,RptJson";
        public const string MainFields = "Id, SwitchNo,MinTime,MinTime,CreateTime,RptJson";

        /// <summary>
        //  自增ID
        /// </summary>
        public long Id { get; set; }
        /// <summary>
        //  开关编号
        /// </summary>
        public string SwitchNo { get; set; }
        /// <summary>
        //  多类试验中最早的
        /// </summary>
        public DateTime MinTime { get; set; }
        /// <summary>
        //  多类试验中最晚的
        /// </summary>
        public DateTime MaxTime { get; set; }

        /// <summary>
        //  创建时间
        /// </summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>
        //  组合报告excel文件
        /// </summary>
        public byte[] RptExcel { get; set; }

        /// <summary>
        //  组合报告信息，源于 SwitchRptModel 对象的Json序列化
        /// </summary>
        public string RptJson { get; set; }
    }
}

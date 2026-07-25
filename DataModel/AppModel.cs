using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CtrlCenter.DataModel
{
    public enum AppType
    {
        HVC,
        ZKC,
        LRT,
    }

    public class AppModel
    {
        public AppType Type { get; set; }
        public string Guid { get; set; }
        public string Name { get; set; }
        public string Exe { get; set; }
        public string Desc { get; set; }
        public string RptPattern { get; set; }
        public string ScanFolder { get; set; }
        public string Location { get; set; }
        public string FullName { get; set; }
        public Process Process { get; set; }
        
        public string ActionText
        {
            get
            {
                if (string.IsNullOrEmpty(FullName))
                    return Type == AppType.HVC ? "搜索app" : "未安装";
                return Process == null ? "启动app" : "关闭app";
            }
        }
        public Func<string, (string, string)> GetTxtAndSwitchNo { get; set; }
        public bool CanEditName { get; set; } = false;        
        public bool CanSelectRptLoc { get; set; } = false;
    }
}
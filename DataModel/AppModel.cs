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
        ZKC,
        LRT,
        HVC,
    }

    public class ExcelCfgModel
    {
        public int Version { get; set; } = 100;
        public bool UseRawSheetName { get; set; }
        public string TemplSheetName { get; set; } = "lrt_hvt_zkc_at";
        public string TitleRange { get; set; } = "A2:M7";
        public int TemplTitleIndex { get; set; } = 9;
        public int TemplRowIndex { get; set; } = 10;
        public int MaxRowOfPage1 { get; set; } = 33;
        public int MaxRowOfPagex { get; set; } = 49;        
    }

    public class AppModel
    {
        public AppType Type { get; set; }
        public string Guid { get; set; }
        public string Name { get; set; }
        public string Exe { get; set; }
        public string Desc { get; set; }
        public string RptPattern { get; set; }
        public string RptFolder { get; set; }
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
        public bool CanSelectRptLoc { get; set; } = false;
        public ExcelCfgModel ExcelCfgModel { get; set; }
    }
}
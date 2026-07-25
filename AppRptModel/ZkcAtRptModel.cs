namespace CtrlCenter.AppRptModel
{
    //用于扫描到rpt文件后快速发现开关编号
    public class ZkcRptSwitchNoModel
    {
        public ZkcRptCfgModel RptCfg { get; set; }
    }

    public class ZkcAtRptModel : ZkcRptSwitchNoModel
    {
        public string TestTime { get; set; }
        public string TestType { get; set; }
        public List<List<string>> Test1RatedPct100 { get; set; }
        public List<List<string>> Test2RcRoPct30 { get; set; }
        public List<List<string>> Test3RcRoPct32 { get; set; }
        public List<List<string>> Test4LowPct8562 { get; set; }
        public List<List<string>> Test5LowPct8565 { get; set; }
        public List<List<string>> Test6High110 { get; set; }
        public List<List<string>> Test7AntiPumping { get; set; }
        public List<List<string>> Test8ReClose { get; set; }
    }
}

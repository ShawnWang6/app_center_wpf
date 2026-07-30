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
        public int GetTotalRowCount()
        {
            int count = 0;
            count +=  Test1RatedPct100 != null ? Test1RatedPct100.Count : 0;             
            count += Test2RcRoPct30 != null ? Test2RcRoPct30.Count : 0;
            count += Test3RcRoPct32 != null ? Test3RcRoPct32.Count : 0;
            count += Test4LowPct8562 != null ? Test4LowPct8562.Count : 0;
            count += Test5LowPct8565 != null ? Test5LowPct8565.Count : 0;
            count += Test6High110 != null ? Test6High110.Count : 0;
            count += Test7AntiPumping != null ? Test7AntiPumping.Count : 0;
            count += Test8ReClose != null ? Test8ReClose.Count : 0;
            return count;
        }
        public IEnumerable<List<string>> GetTotalRows()
        {
            return (Test1RatedPct100 ?? new List<List<string>>())
           .Concat(Test2RcRoPct30 ?? new List<List<string>>())
           .Concat(Test3RcRoPct32 ?? new List<List<string>>())
           .Concat(Test4LowPct8562 ?? new List<List<string>>())
           .Concat(Test5LowPct8565 ?? new List<List<string>>())
           .Concat(Test6High110 ?? new List<List<string>>())
           .Concat(Test7AntiPumping ?? new List<List<string>>())
           .Concat(Test8ReClose ?? new List<List<string>>());
        }
    }
}

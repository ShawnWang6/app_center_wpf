namespace CtrlCenter.AppRptModel
{
    public class ZkcRptCfgModel
    {
        //单位名称
        public string DeptName { get; set; }
        //线路名称
        public string LineName { get; set; }
        //开关编号         
        public string SwitchNo { get; set; }
        //开关型号
        public string SwitchModel { get; set; }
        public bool HasSwitchNo() { return !string.IsNullOrEmpty(SwitchNo); }
        public ZkcRptCfgModel Clone()
        {
            return new ZkcRptCfgModel
            {
                DeptName = DeptName,
                LineName = LineName,
                SwitchNo = SwitchNo,
                SwitchModel = SwitchModel
            };
        }
        public override string ToString()
        {
            return $"Dep[{DeptName}], Line[{LineName}], No[{SwitchNo}], Mode[{SwitchModel}]";
        }
    }
}

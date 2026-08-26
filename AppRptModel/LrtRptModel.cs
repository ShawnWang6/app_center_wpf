namespace CtrlCenter.AppRptModel
{
    public class LrtRptSwitchNoModel
    {
        public string DevId { get; set; }
    }
    public class LrtRptModel : LrtRptSwitchNoModel
    {
        /// <summary>
        //  回路电阻测试仪原始输出json格式
        //js["TestTime"] = Utf8FromCString(log.strTestTime);
        //js["Model"] = Utf8FromCString(log.strModel);
        //js["DevId"] = Utf8FromCString(log.strDevId);
        //js["Ra"] = Utf8FromCString(log.strRa);
        //js["RaOk"] = Utf8FromCString(CSkinTestApp::IsValueOk(log.strRa, di));
        //js["Rb"] = Utf8FromCString(log.strRb);
        //js["RbOk"] = Utf8FromCString(CSkinTestApp::IsValueOk(log.strRb, di));
        //js["Rc"] = Utf8FromCString(log.strRc);
        //js["RcOk"] = Utf8FromCString(CSkinTestApp::IsValueOk(log.strRc, di));
        //js["I"] = Utf8FromCString(log.strI);
        //js["IOk"] = Utf8FromCString(CSkinTestApp::IsValueOk(log.strI, di));
        //js["RangeI"] = Utf8FromCString(log.strRangeI);
        //js["RangeR"] = Utf8FromCString(log.strRangeR);
        //以下没导出到csv
        //js["Temp"] = Utf8FromCString(log.strTemp);
        //js["Tester"] = Utf8FromCString(log.strTester);
        //js["Project"] = Utf8FromCString(log.strProject);
        //js["Beizhu"] = Utf8FromCString(log.strBeizhu);
        //js["LogTime"] = Utf8FromCString(log.strLogTime);
        //js["LogTime"] = Utf8FromCString(log.strLogTime);
        /// </summary>    
        public string TestTime { get; set; }
        public string Model { get; set; }
        //public string DevId { get; set; }
        public string Ra { get; set; }
        public string RaOk { get; set; }
        public string Rb { get; set; }
        public string RbOk { get; set; }
        public string Rc { get; set; }
        public string RcOk { get; set; }
        public string I { get; set; }
        public string IOk { get; set; }
        public string RangeI { get; set; }
        public string RangeR { get; set; }
        //以下没导出到csv
        public string Temp { get; set; }
        public string Tester { get; set; }
        public string Project { get; set; }
        public string Beizhu { get; set; }
        public string LogTime { get; set; }
    }
}

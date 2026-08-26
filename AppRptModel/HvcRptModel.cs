namespace CtrlCenter.AppRptModel
{
    public class HvcRptModel
    {       
        public string TestTime { get; set; }
        public string SwitchNo { get; set; }        
        public string Dc { get; set; } //end with mA
        public string Ac { get; set; } //end with mA
        //Insulation Resistance 绝缘电阻
        public string InsRes { get; set; } //end with GΩ
        
        public string Result { get; set; } //OK
    }
}

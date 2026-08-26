namespace CtrlCenter.DataModel
{
    public class RptFile
    {
        public string SwitchNo { get; set; }
        public long TimeStamp { get; set; }
        public AppType FileType { get; set; }
        public string Content { get; set; }
        public string FileNameLowerCase { get; set; }        
        public string FilePath { get; set; }
    }
}

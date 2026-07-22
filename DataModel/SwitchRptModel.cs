using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CtrlCenter.DataModel
{
    public class RptFileBase
    {
        public long TimeStamp { get; set; }
        public AppType FileType { get; set; }                
        public string Content { get; set; }
        public string FileNameLowerCase { get; set; }
    }

    public class RptFile : RptFileBase
    {
        public string SwitchNo { get; set; }
        public string FilePath { get; set; }
    }


    public class SwitchRptModel
    {
        public RptFileBase[] Files { get; set; }

    }
}

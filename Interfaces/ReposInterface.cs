using CtrlCenter.DataModel;
using CtrlCenter.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CtrlCenter.Interfaces
{
    public interface ISwitchHisRepos
    {
        IList<SwitchHisEntity> GetSwitchHis(string switchNo, DateTime? startTime, DateTime? minTime);
        string SaveSwitchHis(SwitchHisEntity switchHis);
    }

}

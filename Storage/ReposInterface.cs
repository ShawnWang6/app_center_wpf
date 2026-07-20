using CtrlCenter.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CtrlCenter.Storage
{
    public interface ISwitchHisRepos
    {
        IEnumerable<SwitchHisEntity> GetSwitchHis(string switchNo, DateTime? startTime, DateTime? minTime);
        string SaveSwitchHis(SwitchHisEntity switchHis);

    }

}

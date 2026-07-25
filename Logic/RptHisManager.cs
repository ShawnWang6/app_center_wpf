using CtrlCenter.DataModel;
using CtrlCenter.Interfaces;
using CtrlCenter.Storage;
using Newtonsoft.Json;

namespace CtrlCenter.Logic
{

    public class RptHisManager
    {
        private ISwitchHisRepos _hisRepos;
        private readonly IList<SwitchHisEntity> _rptHis = new List<SwitchHisEntity>();
        private readonly IDictionary<string, IList<SwitchHisEntity>> _switchHis = new Dictionary<string, IList<SwitchHisEntity>>();

        public RptHisManager(ISwitchHisRepos hisRepos)
        {
            _hisRepos = hisRepos;
        }

        public IList<SwitchHisEntity> RptHis => _rptHis;

        public string LoadRptHis()
        {
            var err = string.Empty;
            try
            {
                var his = _hisRepos.GetSwitchHis(null, null, null);
                _rptHis.Clear();
                _switchHis.Clear();
                foreach (var item in his)
                {
                    _rptHis.Add(item);
                    if (!_switchHis.TryGetValue(item.SwitchNo, out var list))
                    {
                        _switchHis[item.SwitchNo] = new List<SwitchHisEntity>();
                    }
                    _switchHis[item.SwitchNo].Add(item);
                }

            }
            catch (Exception ex)
            {
                err = ex.Message;
            }
            return err;
        }
        public (bool, string) SaveRptfiles(IDictionary<AppType, RptFile> switchRpts)
        {
            if (switchRpts.Count < 3)
            {
                return (false, "至少需要3个报表文件");
            }
            var switchNo = switchRpts.Values.FirstOrDefault().SwitchNo;
            var switchReport = Util.BuildSwitchHisEntity(switchRpts, switchNo);
            var err = _hisRepos.SaveSwitchHis(switchReport);
            if (!string.IsNullOrEmpty(err))
            {
                return (false, err);
            }

            // Add to in-memory collections
            _rptHis.Add(switchReport);
            if (!_switchHis.TryGetValue(switchNo, out var list))
            {
                _switchHis[switchNo] = new List<SwitchHisEntity>();
            }
            _switchHis[switchNo].Add(switchReport);

            return (true, string.Empty);
        }
    }

}

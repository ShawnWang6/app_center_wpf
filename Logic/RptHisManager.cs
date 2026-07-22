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
        public bool SaveRptfiles(IDictionary<AppType, RptFile> switchRpts)
        {
            if (switchRpts.Count < 2)
            {
                return false;
            }

            // Sort the files by timestamp
            var sortedFiles = switchRpts.Values.OrderBy(v => v.TimeStamp).ToArray();
            var switchNo = sortedFiles.First().SwitchNo;
            var minTime = Util.ParseYyMmDdHhMmSs(sortedFiles.First().TimeStamp);
            var maxTime = Util.ParseYyMmDdHhMmSs(sortedFiles.Last().TimeStamp);

            // Convert sortedFiles to SwitchRptModel
            var rptModel = new SwitchRptModel
            {
                Files = sortedFiles.Select(rpt => new RptFileBase
                {
                    TimeStamp = rpt.TimeStamp,
                    FileType = rpt.FileType,
                    Content = rpt.Content,
                    FileNameLowerCase = rpt.FileNameLowerCase,
                }).ToArray()
            };

            // Serialize the SwitchRptModel to JSON
            var rptJson = JsonConvert.SerializeObject(rptModel);

            // Create a new SwitchHisEntity
            var switchReport = new SwitchHisEntity
            {
                SwitchNo = switchNo,
                RptJson = rptJson,
                MinTime = minTime,
                MaxTime = maxTime
            };

            var err = _hisRepos.SaveSwitchHis(switchReport);
            if (string.IsNullOrEmpty(err))
            {
                return false;
            }

            // Add to in-memory collections
            _rptHis.Add(switchReport);
            if (!_switchHis.TryGetValue(switchNo, out var list))
            {
                _switchHis[switchNo] = new List<SwitchHisEntity>();
            }
            _switchHis[switchNo].Add(switchReport);

            return true;
        }
    }

}

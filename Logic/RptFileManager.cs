using CtrlCenter.DataModel;
using CtrlCenter.Interfaces;
using CtrlCenter.Storage;
using Newtonsoft.Json;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace CtrlCenter.Logic
{
    class RptFileManager
    {
        private readonly AppSetting _appSetting;

        public RptFileManager(AppSetting appSetting)
        {
            _appSetting = appSetting;
        }

        /// <summary>
        //  最近做了试验的报表文件信息
        /// </summary>
        public RptFile Master { get; set; }
        /// <summary>
        //  和Master开关编号一致的最新的报表文件信息
        /// </summary>
        public Dictionary<AppType, RptFile> SwitchFiles { get; set; } = new Dictionary<AppType, RptFile>();

        /// <summary>
        //  扫描到的ScanFileTimeStampMin之内的文件，key为小写文件名(不含路径)
        /// </summary>
        public Dictionary<string, RptFile> LatestFiles { get; set; } = new Dictionary<string, RptFile>();

        void RescanRptFiles(IList<AppModel> apps)
        {
            // Get the current timestamp
            long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Temporary dictionary to store the latest files during this scan
            var newLatestFiles = new Dictionary<string, RptFile>(StringComparer.OrdinalIgnoreCase);

            // Scan each app's ScanFolder
            foreach (var app in apps)
            {
                if (string.IsNullOrEmpty(app.RptFolder) || !Directory.Exists(app.RptFolder))
                {
                    Log.Warning($"ScanFolder does not exist for app: {app.Name}");
                    continue;
                }

                // Get all files in the ScanFolder
                var files = Directory.GetFiles(app.RptFolder, app.RptPattern);
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    fileName = fileName.ToLower();
                    var fileTimestamp = GetTimestampFromFileName(fileName);
                    if (fileTimestamp == null) continue;
                    if (currentTimestamp - fileTimestamp.Value > _appSetting.ScanFileMaxTimeSpanSec)
                    {
                        continue;
                    }

                    // Check if the file already exists in the original LatestFiles
                    if (LatestFiles.TryGetValue(fileName, out var existingRptFile))
                    {
                        newLatestFiles[fileName] = existingRptFile;
                    }
                    else
                    {
                        // Create a new RptFile object
                        var (content, switchNo) = app.GetTxtAndSwitchNo(file);
                        var newRptFile = new RptFile
                        {
                            TimeStamp = fileTimestamp.Value,
                            FileType = app.Type,
                            SwitchNo = switchNo,
                            FilePath = file,
                            Content = content,
                            FileNameLowerCase = fileName

                        };
                        newLatestFiles[fileName] = newRptFile;
                    }
                }
            }

            // Update LatestFiles with the new scan results
            LatestFiles = newLatestFiles;
        }

        public void RefreshAppRptFiles(IList<AppModel> apps, RptFile rpt = null)
        {
            if (rpt == null)
            {
                RescanRptFiles(apps);
            }
            else
            {
                long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (currentTimestamp - rpt.TimeStamp > _appSetting.ScanFileMaxTimeSpanSec)
                {
                    return;
                }
                if (LatestFiles.ContainsKey(rpt.FileNameLowerCase))
                {
                    return;
                }
                LatestFiles.Add(rpt.FileNameLowerCase, rpt);
            }

            var newMaster = LatestFiles.Values.OrderByDescending(r => r.TimeStamp).FirstOrDefault();
            if (newMaster == null || Master == null)
            {
                if (newMaster != null || Master != null)
                {
                    Master = newMaster;
                }
            }
            else if (Master.TimeStamp != newMaster.TimeStamp || Master.FilePath != newMaster.FilePath)
            {
                Master = newMaster;
                Log.Information($"Master updated: {Master.FilePath}");
            }

            if (Master == null)
            {
                SwitchFiles.Clear();
            }
            else
            {
                var result = LatestFiles.Values
                    .Where(rpt => rpt.FileType != Master.FileType && rpt.SwitchNo == Master.SwitchNo)
                    .GroupBy(rpt => rpt.FileType)
                    .Select(group => group.OrderByDescending(rpt => rpt.TimeStamp).First())
                    .ToDictionary(gp => gp.FileType, gp => gp);
                result[Master.FileType] = Master;
                SwitchFiles = result;
            }
        }

        /// <summary>
        /// Extracts the timestamp from the file name.
        /// Assumes the timestamp is the first part of the file name, separated by '_'.
        /// </summary>
        /// <param name="fileName">The file name to extract the timestamp from.</param>
        /// <returns>The timestamp as a long, or null if invalid.</returns>

        private static readonly Regex _timestampRegex = new Regex(@"^(?<timestamp>\d{2}(?:0[1-9]|1[0-2])(?:0[1-9]|[12]\d|3[01])\d{6}).*\.(?:rpt|csv)$", RegexOptions.Compiled);
        private static long? GetTimestampFromFileName(string fileName)
        {
            Match match = _timestampRegex.Match(fileName);
            if (match.Success)
            {
                return long.Parse(match.Groups["timestamp"].Value);
            }
            return null;
        }

        public static RptFile GetAppNewRptFile(Func<string, (string, string)> getTxtAndSwitchNo,
            string appName, AppType appType, string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            Match match = _timestampRegex.Match(fileName);
            if (!match.Success)
            {
                Log.Warning($"线程[{Thread.CurrentThread.ManagedThreadId}] 未能识{appName}报表文件: {filePath}");
                return null;
            }
            var (content, switchNo) = getTxtAndSwitchNo(filePath);
            if (string.IsNullOrEmpty(switchNo))
            {
                Log.Warning($"线程[{Thread.CurrentThread.ManagedThreadId}] 未能识{appName} 文件{filePath}的开关编号");
                return null;
            }
            var fileTimestamp = GetTimestampFromFileName(fileName);
            return new RptFile
            {
                TimeStamp = fileTimestamp.Value,
                FileType = appType,
                SwitchNo = switchNo,
                FilePath = filePath,
                Content = content,
                FileNameLowerCase = fileName.ToLower()
            };
        }

    }

}

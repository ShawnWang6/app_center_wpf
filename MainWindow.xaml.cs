using CtrlCenter.DataModel;
using CtrlCenter.Storage;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;

using File = System.IO.File;

namespace CtrlCenter
{
    public partial class MainWindow : Window
    {
        AppInfo[] _apps;
        ManagementEventWatcher _appWatcher;
        HashSet<AppInfo> runningApp = new HashSet<AppInfo>();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            // Load ShowAppLocation from appsettings.json
            ShowAppLocation = bool.TryParse(App.Configuration["ShowAppLocation"], out var showAppLocation) && showAppLocation;
        }

        public static readonly DependencyProperty ShowAppLocationProperty =
            DependencyProperty.Register(nameof(ShowAppLocation), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

        public bool ShowAppLocation
        {
            get => (bool)GetValue(ShowAppLocationProperty);
            set => SetValue(ShowAppLocationProperty, value);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _apps = new[]
            {
                    new AppInfo
                    {
                        Type = AppType.ZKC,
                        Guid = "{B41B0EBC-95F3-45A5-AE4C-4A40696C198D}_is1",
                        Name = "ZKC1601S开关机械特性综合测试系统",
                        Exe = "ZKC1601S",
                        GetTxtAndSwitchNo = AppInfo.GetTxtAndNoFromZkc,
                        RptPattern = "????????????_*.rpt",
                    },
                    new AppInfo
                    {
                        Type = AppType.IR,
                        Guid = "{28692C18-A1DF-465B-9359-42C6F601243A}_is1",
                        Name = "三通道回路电阻测试仪后台软件",
                        Exe = "IRTest",
                        GetTxtAndSwitchNo = AppInfo.GetTxtAndNoFromIr,
                        RptPattern = "????????????_ir*.rpt",
                    },
                    new AppInfo
                    {
                        Type = AppType.HVC,
                        Guid = string.Empty,
                        Name = "高压线缆测试系统",
                        Exe = "HighVoltCableTestSystem",
                        GetTxtAndSwitchNo = AppInfo.GetTxtAndNoFromHvc,
                        RptPattern = "????????????*.csv",
                    }
                };

            _apps[2].FullName = Util.LoadAppPath(_apps[2].Exe);
            if (!string.IsNullOrEmpty(_apps[2].FullName))
            {
                _apps[2].Location = Path.GetDirectoryName(_apps[2].FullName);
            }

            for (int index = 0; index < _apps.Length; index++)
            {
                var app = _apps[index];
                app.Index = index;
                string location;
                if (!string.IsNullOrEmpty(app.Guid) && Util.TryGetInstallLocationByGuid(app.Guid, out location))
                {
                    app.Location = location;
                }

                if (!string.IsNullOrEmpty(app.Location))
                {
                    app.FullName = Path.Combine(app.Location, app.Exe + ".exe");
                    app.Process = Util.GetProcess(app.FullName);
                    MonitorProcess(app);
                }

                if (string.IsNullOrEmpty(app.Guid))
                {
                    // app3 requires user to set ScanFolder
                    app.ScanFolder = Util.LoadAppPath(app.Exe + "_ScanFolder");
                }
                else if (!string.IsNullOrEmpty(app.Location))
                {
                    app.ScanFolder = Path.Combine(app.Location, "sync");
                }

                // Ensure ScanFolder exists
                if (!string.IsNullOrEmpty(app.ScanFolder) && !Directory.Exists(app.ScanFolder))
                {
                    Directory.CreateDirectory(app.ScanFolder);
                }
                UpdateAppActionCell(app);
            }

            dataGridApps.ItemsSource = _apps;
            _appWatcher = MonitorApps(_apps);

            rptFileManager.RefreshAppRptFiles(_apps);
            listViewExperimentOutput.ItemsSource = rptFileManager.SwitchFiles.Values.OrderBy(o => o.TimeStamp).ToList();
            MonitorScanFolders(_apps);
        }

        private void SetScanFolder_Click(object sender, RoutedEventArgs e)
        {
            var app = (sender as FrameworkElement)?.DataContext as AppInfo;
            if (app == null) return;

            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Select a folder to monitor";
                dlg.RootFolder = Environment.SpecialFolder.MyComputer;
                dlg.ShowNewFolderButton = true;

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    app.ScanFolder = dlg.SelectedPath;
                    Util.SaveAppPath(app.Exe + "_ScanFolder", app.ScanFolder); // Save to registry
                }
            }
        }

        private readonly List<FileSystemWatcher> _watchers = new();
        private void MonitorScanFolders(AppInfo[] apps)
        {
            foreach (var app in apps)
            {
                if (string.IsNullOrEmpty(app.ScanFolder)) continue;

                Debug.WriteLine($"Monitoring directory: {app.ScanFolder}");
                var watcher = new FileSystemWatcher(app.ScanFolder)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    Filter = "*.*",
                    EnableRaisingEvents = true
                };

                watcher.Created += (s, e) =>
                {
                    try
                    {
                        Debug.WriteLine($"File created: {e.FullPath}");
                        ProcessNewFile(app, e.FullPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in ProcessNewFile: {ex.Message}");
                    }
                };
                _watchers.Add(watcher); // Keep a reference to prevent garbage collection
            }
        }

        // Existing code...

        private void ProcessNewFile(AppInfo app, string filePath)
        {
            RptFile rpt = RptFileManager.GetAppNewRptFile(app.GetTxtAndSwitchNo, app.Name, app.Type, filePath);
            if (rpt == null) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                rptFileManager.RefreshAppRptFiles(_apps, null, rpt);
                listViewExperimentOutput.ItemsSource = rptFileManager.SwitchFiles.Values.OrderBy(o => o.TimeStamp).ToList();
            }));
        }
        private RptFileManager rptFileManager = new RptFileManager();
        

        void UpdateAppActionCell(AppInfo app)
        {
            app.NotifyActionChanged();
        }

        

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            var app = (sender as FrameworkElement)?.DataContext as AppInfo;
            if (app == null) return;

            if (app.Process != null)
            {
                WindowActivator.ActivateWindow(app.Process, true);
            }
            else if (!string.IsNullOrEmpty(app.FullName))
            {
                Util.StartApp(app.FullName);
            }
            else if (app.Index == 2)
            {
                OnBrowseApp(app);
            }
        }

        void OnBrowseApp(AppInfo app)
        {
            string folder = null;
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择程序的安装目录";
                dlg.RootFolder = Environment.SpecialFolder.MyComputer;
                dlg.ShowNewFolderButton = false;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    folder = dlg.SelectedPath;
                }
            }

            if (string.IsNullOrEmpty(folder)) return;

            var files = new List<string>();
            foreach (string file in Util.FindFilesEnumerable(folder, $"{app.Exe}.exe"))
            {
                files.Add(file);
                break;
            }

            if (files.Count == 0)
            {
                System.Windows.MessageBox.Show($"未能找到目标文件 {app.Exe}.exe", "提示");
                return;
            }

            app.FullName = files.First();
            Util.SaveAppPath(app.Exe, app.FullName);
            app.Location = Path.GetDirectoryName(app.FullName);
            app.Process = Util.GetProcess(app.FullName);
            UpdateAppActionCell(app);
            MonitorProcess(app);
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            var process = sender as Process;
            if (process == null) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var app = runningApp.FirstOrDefault(o => o.Process != null && o.Process.Id == process.Id);
                if (app == null || app.Process == null) return;

                app.Process.Exited -= OnProcessExited;
                app.Process.Dispose();
                app.Process = null;
                runningApp.Remove(app);
                UpdateAppActionCell(app);
                Debug.WriteLine($"[OnProcessExited线程:{System.Threading.Thread.CurrentThread.ManagedThreadId,2}] [{app.Exe}] 已经退出");
            }));
        }

        private bool MonitorProcess(AppInfo app)
        {
            if (app == null || app.Process == null) return false;
            if (runningApp.Contains(app)) return false;

            runningApp.Add(app);
            app.Process.EnableRaisingEvents = true;
            app.Process.Exited += OnProcessExited;

            if (app.Process.HasExited)
            {
                OnProcessExited(app.Process, EventArgs.Empty);
            }

            return true;
        }

        ManagementEventWatcher MonitorApps(AppInfo[] apps)
        {
            var targetProcesses = apps.Select(o => $"{o.Exe}.exe").ToArray();
            string processFilter = string.Join("' OR ProcessName = '", targetProcesses);
            string queryString = $"SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName = '{processFilter}'";

            WqlEventQuery query = new WqlEventQuery(queryString);
            ManagementEventWatcher watcher = new ManagementEventWatcher(query);
            watcher.EventArrived += (sender, eventArgs) =>
            {
                var process = Util.GetProcess(eventArgs);
                if (process == null) return;

                string processPath = null;
                try
                {
                    processPath = process.MainModule.FileName;
                }
                catch
                {
                    return;
                }

                if (!string.IsNullOrEmpty(processPath))
                {
                    foreach (var app in apps)
                    {
                        if (string.IsNullOrEmpty(app.FullName)) continue;
                        if (!string.Equals(processPath, app.FullName, StringComparison.OrdinalIgnoreCase)) continue;

                        var proc = process;
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            app.Process = proc;
                            UpdateAppActionCell(app);
                            MonitorProcess(app);
                        }));
                        process = null;
                        break;
                    }
                }

                if (process != null) process.Dispose();
            };
            watcher.Start();
            return watcher;
        }
    }

    


    class AppInfo : System.ComponentModel.INotifyPropertyChanged
    {
        public AppType Type { get; set; }        
        public string Guid { get; set; }
        public string Name { get; set; }
        public string Exe { get; set; }
        public string Desc { get; set; }
        public string RptPattern { get; set; }
        public string _scanFolder;
        public string ScanFolder
        {
            get => _scanFolder;
            set
            {
                _scanFolder = value;
                OnPropertyChanged(nameof(ScanFolder));
            }
        }

        private string _location;
        public string Location
        {
            get => _location;
            set { _location = value; 
                OnPropertyChanged(nameof(Location)); 
                OnPropertyChanged(nameof(ActionText)); 
                OnPropertyChanged(nameof(ActionForeground)); }
        }

        private string _fullName;
        public string FullName
        {
            get => _fullName;
            set { _fullName = value; 
                OnPropertyChanged(nameof(FullName)); 
                OnPropertyChanged(nameof(ActionText)); 
                OnPropertyChanged(nameof(ActionForeground)); }
        }

        private Process _process;
        public Process Process
        {
            get => _process;
            set { _process = value; 
                OnPropertyChanged(nameof(Process));
                OnPropertyChanged(nameof(ActionText)); 
                OnPropertyChanged(nameof(ActionForeground)); }
        }

        //所在UI行索引
        public int Index { get; set; }

        public string ActionText
        {
            get
            {
                if (string.IsNullOrEmpty(FullName))
                    return Index == 2 ? "扫描" : "未安装";
                return Process == null ? "启动" : "关闭";
            }
        }

        public System.Windows.Media.Brush ActionForeground
        {
            get
            {
                if (string.IsNullOrEmpty(FullName))
                    return System.Windows.Media.Brushes.Black;
                return Process == null ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        public void NotifyActionChanged()
        {
            OnPropertyChanged(nameof(ActionText));
            OnPropertyChanged(nameof(ActionForeground));
        }

        public static (string, string) GetTxtAndNoFromZkc(string filePath)
        {
            var json = File.ReadAllText(filePath);            
            dynamic data = JsonConvert.DeserializeObject(json);
            return (json, data?.RptCfg?.SwitchNo);
        }
        public static (string, string) GetTxtAndNoFromIr(string filePath)
        {
            var json = File.ReadAllText(filePath);
            dynamic data = JsonConvert.DeserializeObject(json);            
            return (json, data?.DevId);
        }

        public static (string, string) GetTxtAndNoFromHvc(string filePath)
        {
            // Handle CSV file (app3)
            //TODO  support GBK
            var lines = File.ReadAllLines(filePath, System.Text.Encoding.ASCII);
            if (lines.Length > 0)
            {
                return (lines[0], lines[0].Split(',')[0]); // First column is the switch number
            }
            return (null, null);
        }
        public Func<string, (string, string)> GetTxtAndSwitchNo;
    }

    public class EmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    
    class RptFileManager
    {
        /// <summary>
        //  最近做了试验的报表文件信息
        /// </summary>
        public RptFile Master { get; set; }
        /// <summary>
        //  和Master开关编号一致的最新的报表文件信息
        /// </summary>
        public Dictionary<AppType, RptFile> SwitchFiles { get; set; } = new Dictionary<AppType, RptFile>();


        /// <summary>
        //  仅描扫描改时间戳据当前时间最大的时间间隔(单位秒), 默认只扫描最近5分钟的报表文件
        /// </summary>
        public long ScanFileMaxTimeSpanSec { get; set; } = 300;

        

        /// <summary>
        //  扫描到的ScanFileTimeStampMin之内的文件，key为小写文件名(不含路径)
        /// </summary>
        public Dictionary<string, RptFile> LatestFiles { get; set; } = new Dictionary<string, RptFile>();

        void RescanRptFiles(AppInfo[] apps)
        {
            // Get the current timestamp
            long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Temporary dictionary to store the latest files during this scan
            var newLatestFiles = new Dictionary<string, RptFile>(StringComparer.OrdinalIgnoreCase);

            // Scan each app's ScanFolder
            foreach (var app in apps)
            {
                if (string.IsNullOrEmpty(app.ScanFolder) || !Directory.Exists(app.ScanFolder))
                {
                    Debug.WriteLine($"ScanFolder does not exist for app: {app.Name}");
                    continue;
                }

                // Get all files in the ScanFolder
                var files = Directory.GetFiles(app.ScanFolder, app.RptPattern);
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    fileName = fileName.ToLower();
                    var fileTimestamp = GetTimestampFromFileName(fileName);
                    if (fileTimestamp == null) continue;
                    if (currentTimestamp - fileTimestamp.Value > ScanFileMaxTimeSpanSec)
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

        void RescanRptFiles(IList<AppModel> apps)
        {
            // Get the current timestamp
            long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Temporary dictionary to store the latest files during this scan
            var newLatestFiles = new Dictionary<string, RptFile>(StringComparer.OrdinalIgnoreCase);

            // Scan each app's ScanFolder
            foreach (var app in apps)
            {
                if (string.IsNullOrEmpty(app.ScanFolder) || !Directory.Exists(app.ScanFolder))
                {
                    Debug.WriteLine($"ScanFolder does not exist for app: {app.Name}");
                    continue;
                }

                // Get all files in the ScanFolder
                var files = Directory.GetFiles(app.ScanFolder, app.RptPattern);
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    fileName = fileName.ToLower();
                    var fileTimestamp = GetTimestampFromFileName(fileName);
                    if (fileTimestamp == null) continue;
                    if (currentTimestamp - fileTimestamp.Value > ScanFileMaxTimeSpanSec)
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

        public void RefreshAppRptFiles(AppInfo[] apps, IList<AppModel> appsV2 = null, RptFile rpt = null)
        {
            if (rpt == null)
            {
                if (apps != null)
                {
                    RescanRptFiles(apps);
                }
                if (appsV2 != null)
                {
                    RescanRptFiles(appsV2);
                }
            }
            else
            {
                long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (currentTimestamp - rpt.TimeStamp > ScanFileMaxTimeSpanSec)
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
                Debug.WriteLine($"Master updated: {Master.FilePath}");
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

        private static readonly Regex _timestampRegex = new Regex(@"^(?<timestamp>\d{2}(?:0[1-9]|1[0-2])(?:0[1-9]|[12]\d|3[01])[01]\d[0-5]\d[0-5]\d).*\.(?:rpt|csv)$", RegexOptions.Compiled);
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
                Debug.WriteLine($"线程[{Thread.CurrentThread.ManagedThreadId}] 未能识{appName}报表文件: {filePath}");
                return null;
            }
            var (content, switchNo) = getTxtAndSwitchNo(filePath);
            if (string.IsNullOrEmpty(switchNo))
            {
                Debug.WriteLine($"线程[{Thread.CurrentThread.ManagedThreadId}] 未能识{appName} 文件{filePath}的开关编号");
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

    class SwitchReport
    {
        public string SwitchNo { get; set; } 
        public RptFile[] Reports { get; set; }  = new RptFile[3];
        public DateTime BeginTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    

    class RptHisManager
    {
        private SwitchHisRepos _hisRepos;
        private readonly IList<SwitchHisEntity> _rptHis = new List<SwitchHisEntity>();
        private readonly IDictionary<string, IList<SwitchHisEntity>> _switchHis = new Dictionary<string, IList<SwitchHisEntity>>();
        
        public RptHisManager(SwitchHisRepos hisRepos)
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
            catch(Exception ex)
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

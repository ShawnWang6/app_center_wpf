using ClosedXML;
using CtrlCenter.AppRptModel;
using CtrlCenter.DataModel;
using CtrlCenter.Excel;
using CtrlCenter.Interfaces;
using CtrlCenter.Logic;
using CtrlCenter.Storage;
using CtrlCenter.Tools;
using DocumentFormat.OpenXml.EMMA;
using DocumentFormat.OpenXml.Wordprocessing;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace CtrlCenter.ViewModel
{
    public class MainViewModel : INotifyPropertyChanged
    {        
        private readonly ISwitchHisRepos _switchHisRepos;
        private readonly AppModel[] _appModels;
        private readonly RptFileManager _rptFileManager;
        private readonly HashSet<AppViewModel> _runningApp = new HashSet<AppViewModel>();        
        private readonly IDictionary<AppType, FileSystemWatcher> _rptWatchers = new Dictionary<AppType, FileSystemWatcher>();
        private readonly ManagementEventWatcher _appWatcher;        
        private readonly RptHisManager _rptHisManager;
        private readonly AppSetting _appSetting;
        public readonly bool _hasXlsAssociatedApp = Util.HasAssociatedApp(".xlsx");
        private readonly string ExcelRptTemplate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "template.xlsx");
        public bool TopMost
        {
            get => _appSetting.TopMost;
            set
            {
                if (_appSetting.TopMost != value)
                {
                    _appSetting.TopMost = value;
                    Util.SaveAppSetting(_appSetting);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TopMostBtnText));
                }
            }
        }
        public string AppTitle
        {
            get => _appSetting.AppTitle?? "高压开关试验报表管理平台";
            set
            {
                if (_appSetting.AppTitle != value)
                {
                    _appSetting.AppTitle = value;
                    OnPropertyChanged();
                }
            }
        }
        public bool ShowSimTool
        {
            get => _appSetting.Debug??false;
            set
            {
                if (_appSetting.Debug != value)
                {
                    _appSetting.Debug = value;
                    OnPropertyChanged();
                }
            }
        }
        public string TopMostBtnText => TopMost ? "取消置顶": "窗口置顶";
        public bool ShowRptName => false;

        public ObservableCollection<AppViewModel> Apps { get; set; } = new ObservableCollection<AppViewModel>();
        public ObservableCollection<RptFileViewModel> RptFiles { get; set; } = new ObservableCollection<RptFileViewModel>();
        public ObservableCollection<RptHisViewModel>  RptHis { get; set; } = new ObservableCollection<RptHisViewModel>();

        private IList<RptHisViewModel> _selectedRptHis { get; set; } = new  List<RptHisViewModel>();

        public bool CanOpendMergedRpt { get; set; }
        public bool CanExportMergedRpt { get; set; }
        public IList<RptHisViewModel> SelectedRptHis
        {
            get => _selectedRptHis;
            set
            {
                if (_selectedRptHis != value)
                {
                    _selectedRptHis = value;
                    CanOpendMergedRpt = _selectedRptHis.Count == 1;
                    CanExportMergedRpt = _selectedRptHis.Count >= 1;
                    OnPropertyChanged(nameof(CanOpendMergedRpt));
                    OnPropertyChanged(nameof(CanExportMergedRpt));
                }
            }
        }

        public ICommand ToggleTopmostCommand { get; }
        public ICommand AppSettingCommand { get; }
        public ICommand SimAppRptCommand { get; }
        public ICommand SelectAppLocCommand { get; }
        public ICommand DynamicActionCommand { get; }
        public ICommand MergeRptCommand { get; }
        public ICommand PreviewMergedRptCommand { get; }
        public ICommand RefreshRptCommand { get; }
        public ICommand OpenMergedRptCommand { get; }
        public ICommand ExportMergedRptCommand { get; }
        public bool CanMergeRpt => RptFiles.Count >= 3;
        public bool CanPreviewMergedRpt => RptFiles.Count >= 3;
        private AppModel[] InitApps()
        {
            var apps = new AppModel[]
            {
                new AppModel
                {
                        Type = AppType.ZKC,
                        Guid = "{B15AE66C-F969-4402-BF2E-D719FE6B9DC2}}_is1",
                        //Name = "ZKC1601开关机械特性综合测试系统",
                        Name = " 机械特性测试仪[ZKC]",
                        Exe = "ZKC2601",
                        GetTxtAndSwitchNo = Util.GetTxtAndNoFromZkc,
                        RptPattern = "????????????_*.rpt",
                 },
                 new AppModel
                    {
                        Type = AppType.LRT,
                        Guid = "{28692C18-A1DF-465B-9359-42C6F601243A}_is1",
                        //Name = "三通道回路电阻测试仪后台软件",
                        Name = " 回路电阻测试仪[LRT]",
                        Exe = "IRTest",
                        GetTxtAndSwitchNo = Util.GetTxtAndNoFromLrt,
                        RptPattern = "????????????_ir*.rpt",
                    },
                    new AppModel
                    {
                        Type = AppType.HVC,
                        Guid = string.Empty,
                        //Name = "高压线缆测试系统",
                        Name = " 高压线缆测试仪[HVC]",
                        Exe = "HighVoltCableTestSystem",
                        GetTxtAndSwitchNo = Util.GetTxtAndNoFromHvc,
                        RptPattern = "????????????*.csv",
                        CanSelectRptLoc = true,
                    }
            };


            apps[2].FullName = Util.LoadAppPath(apps[2].Exe);
            if (!string.IsNullOrEmpty(apps[2].FullName))
            {
                apps[2].Location = Path.GetDirectoryName(apps[2].FullName);
            }
            return apps;
        }

        public string _simSelectSwitchNo = "AAA";
        public string SimSelectSwitchNo
        {
            get => _simSelectSwitchNo;
            set
            {
                if (_simSelectSwitchNo != value)
                {
                    _simSelectSwitchNo = value;
                    OnPropertyChanged();
                    // 在这里处理选中省份的逻辑，比如更新关联数据
                }
            }
        }
        public IList<string> SimSwitchNos { get; set; } = new List<string>() { "AAA","BBB", "CCC", "DDD","EEE" };
        public IList<AppType> SimsAppTypes { get; set; } = new List<AppType>() { AppType.ZKC, AppType.LRT, AppType.HVC };
        public AppType _sSimSelectAppType = AppType.ZKC;
        public AppType SimSelectAppType
        {
            get => _sSimSelectAppType;
            set
            {
                if (_sSimSelectAppType != value)
                {
                    _sSimSelectAppType = value;
                    OnPropertyChanged();
                    // 在这里处理选中省份的逻辑，比如更新关联数据
                }
            }
        }

        private void InitAppViewModels(AppModel[] apps)
        {
            foreach (var app in apps)
            {
                Apps.Add(new AppViewModel(app));
            }

            foreach (var model in Apps)
            {
                var app = model.Model;
                string location;
                if (!string.IsNullOrEmpty(app.Guid) && Util.TryGetInstallLocationByGuid(app.Guid, out location))
                {
                    app.Location = location;
                }

                if (!string.IsNullOrEmpty(app.Location))
                {
                    app.FullName = Path.Combine(app.Location, app.Exe + ".exe");
                    app.Process = Util.GetProcess(app.FullName);
                    MonitorProcess(model);
                }

                if (string.IsNullOrEmpty(app.Guid))
                {   
                    app.RptFolder = Util.LoadAppPath(model.RptFolderRegKey);
                }
                else if (!string.IsNullOrEmpty(app.Location))
                {
                    app.RptFolder = Path.Combine(app.Location, "sync");
                }

                // Ensure ScanFolder exists
                if (!string.IsNullOrEmpty(app.RptFolder) && !Directory.Exists(app.RptFolder))
                {
                    Directory.CreateDirectory(app.RptFolder);
                }
            }
        }
        IDictionary<string, DateTime> _latestMergedRpts = new Dictionary<string, DateTime>();
        /// <summary>
        //  1. 启动时刷新
        //  2. 手动刷新
        //  3. 扫描到新手动刷新
        //  4. 合并后缓存ScanRptsMaxTimeSpan时间内的文件，若再显示则添加提示(已合并)
        /// </summary>
        void RefreshSwitchRptFiles()
        {
            RptFiles.Clear();
            foreach (var file in _rptFileManager.SwitchFiles.Values)
            {
                bool merged = _latestMergedRpts.ContainsKey(file.FileNameLowerCase);
                RptFiles.Add(new RptFileViewModel(file, merged));
            }
            OnPropertyChanged(nameof(CanMergeRpt));
            OnPropertyChanged(nameof(CanPreviewMergedRpt));
        }
        ManagementEventWatcher MonitorApps(IList<AppViewModel> apps)
        {
            var targetProcesses = apps.Select(o => $"{o.Model.Exe}.exe").ToArray();
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
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            app.Process = proc;
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
        
        private void MonitorScanFolders(IList<AppViewModel> apps)
        {
            foreach (var app in apps)
            {
                if (string.IsNullOrEmpty(app.RptFolder)) continue;

                if (_rptWatchers.ContainsKey(app.Model.Type)) continue;
                Log.Information($"Monitor: {app.RptFolder}");
                var watcher = new FileSystemWatcher(app.RptFolder)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    Filter = app.RptPattern.Remove(0, app.RptPattern.Length - 5),
                    EnableRaisingEvents = true,
                };

                
                watcher.Created += (s, e) => OnFileCreated(app, e);
                watcher.Changed += (s, e) => OnFileChanged(app, e);
                watcher.Error += OnWatcherError;
                // Keep a reference to prevent garbage collection
                _rptWatchers[app.Model.Type] = watcher;
            }
        }
        private void OnFileCreated(AppViewModel model, FileSystemEventArgs e)
        {
            // 延迟一下，等待文件写入完成
            Log.Information($"Detect[{model.Name}]created report: {Path.GetFileName(e.FullPath)}");
        }

        private void OnFileChanged(AppViewModel model, FileSystemEventArgs e)
        {
            //文件修改时，Changed 事件可能会触发多次（因为写入过程中多次写磁盘），
            //建议在事件处理中加入防抖逻辑（比如延迟 500ms 再处理）。
            // 延迟一下，等待文件写入完成
            Log.Information($"Detect[{model.Name}] edited report: {Path.GetFileName(e.FullPath)}");
            Task.Delay(500).ContinueWith(_ =>
            {
                try
                {
                    Log.Information($"File changed: {e.FullPath}");
                    ProcessNewFile(model, e.FullPath);
                }
                catch (Exception ex)
                {
                    Log.Error($"Error in Process changed file[{e.FullPath}]: {ex.Message}");
                }
            });
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            Log.Error($"❌ 监控错误: {e.GetException().Message}");
        }
        private void ProcessNewFile(AppViewModel model, string filePath)
        {
            var app = model.Model;
            RptFile rpt = RptFileManager.GetAppNewRptFile(app.GetTxtAndSwitchNo, app.Name, app.Type, filePath);
            if (rpt == null) return;
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                _rptFileManager.RefreshAppRptFiles(_appModels, rpt);
                RefreshSwitchRptFiles();
            }));
        }
        private bool MonitorProcess(AppViewModel app)
        {
            if (app == null || app.Process == null) return false;
            if (_runningApp.Contains(app)) return false;

            _runningApp.Add(app);
            app.Process.EnableRaisingEvents = true;
            app.Process.Exited += OnProcessExited;

            if (app.Process.HasExited)
            {
                OnProcessExited(app.Process, EventArgs.Empty);
            }

            return true;
        }
        private void OnProcessExited(object sender, EventArgs e)
        {
            var process = sender as Process;
            if (process == null) return;

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var app = _runningApp.FirstOrDefault(o => o.Process != null && o.Process.Id == process.Id);
                if (app == null || app.Process == null) return;

                app.Process.Exited -= OnProcessExited;
                app.Process.Dispose();
                app.Process = null;
                _runningApp.Remove(app);
                Log.Warning($"[OnProcessExited线程:{Thread.CurrentThread.ManagedThreadId,2}] [{app.Model.Exe}] 已经退出");
            }));
        }

        public MainViewModel(ISwitchHisRepos switchHisRepos, AppSetting appSetting)
        {
            _switchHisRepos = switchHisRepos;
            _appSetting = appSetting;
            _rptFileManager = new RptFileManager(appSetting);
            _rptHisManager = new RptHisManager(_switchHisRepos, appSetting);
            _appModels = InitApps();
            InitAppViewModels(_appModels);
            _appWatcher = MonitorApps(Apps);
            _rptFileManager.RefreshAppRptFiles(_appModels.ToList());
            RefreshSwitchRptFiles();
            MonitorScanFolders(Apps);
            _rptHisManager.LoadRptHis(); //TODO init it on app start OnStartup

            //bing mode to view model
            RptHis.Clear();
            foreach (var entity in _rptHisManager.RptHis)
            {
                RptHis.Add(new RptHisViewModel(entity));
            }

            ToggleTopmostCommand = new RelayCommand<MainViewModel> (ExecuteToggleTopmost);
            AppSettingCommand = new RelayCommand<MainViewModel>(ExecuteAppSetting);
            SimAppRptCommand = new RelayCommand<MainViewModel>(ExecuteSimAppRpt);            
            SelectAppLocCommand = new RelayCommand<AppViewModel>(ExecuteSelectAppLoc);
            DynamicActionCommand = new RelayCommand<AppViewModel>(ExecuteDynamicAction);

            MergeRptCommand = new RelayCommand<ObservableCollection<RptFileViewModel>>(ExecuteMergeRpt);
            PreviewMergedRptCommand = new RelayCommand<ObservableCollection<RptFileViewModel>>(ExecutePreviewMergedRpt);
            RefreshRptCommand = new RelayCommand<ObservableCollection<RptFileViewModel>>(ExecuteRefreshRptCommand);

            OpenMergedRptCommand = new RelayCommand<ObservableCollection<RptHisViewModel>>(ExecuteOpenMergedRpt);
            ExportMergedRptCommand = new RelayCommand<ObservableCollection<RptHisViewModel>>(ExecuteExportMergedRpt);
        }
        private void ExecuteToggleTopmost(MainViewModel myself)
        {
            TopMost = !TopMost;
        }
        private void ExecuteAppSetting(MainViewModel myself)
        {
            
        }
        private void ExecuteSimAppRpt(MainViewModel myself)
        {
            var app = _appModels[(int)SimSelectAppType];
            Log.Debug($"模拟报表 {SimSelectSwitchNo}/{SimSelectAppType}/{app.RptFolder}");
            var rptFolder = app.RptFolder;
            if (string.IsNullOrEmpty(rptFolder)) return;
            if (SimSelectAppType == AppType.LRT)
            {
                Util.SimLrtReport(rptFolder, SimSelectSwitchNo);

            }
            if (SimSelectAppType == AppType.ZKC)
            {
                Util.SimZkcReport(rptFolder, SimSelectSwitchNo);
            }
            if (SimSelectAppType == AppType.HVC)
            {
                Util.SimHvcReport(rptFolder, SimSelectSwitchNo);
            }
        }
        private void ExecuteMergeRpt(ObservableCollection<RptFileViewModel> rptFiles)
        {
            var mergedTypes = RptFiles.Where(rpt => rpt.Merged == "是").Select(rpt => rpt.FileType).ToList();
            if (mergedTypes.Count > 0)
            {
                string result = string.Join(",", mergedTypes);
                if(System.Windows.MessageBox.Show($"以下报表已经合并过: {result}, 继续合并?", "提示", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            var (ok, err, entity) = _rptHisManager.SaveRptfiles(_rptFileManager.SwitchFiles);
            if (ok)
            {
                //更新UI合并历史
                RptHis.Add(new RptHisViewModel(entity));

                //标记已合并，缓存当前报表文件名和时间戳
                foreach (var rpt in _rptFileManager.SwitchFiles.Values)
                {
                    _latestMergedRpts[rpt.FileNameLowerCase] = DateTime.Now;
                }
                //刷新列表，标记已合并
                RefreshSwitchRptFiles();

            }
            System.Windows.MessageBox.Show($"合并报表: {(ok ? "成功" : $"失败:{err}")}");
        }
        string TryOpenRptXls(string fileName)
        {
            try
            {
                // 尝试使用系统默认关联程序打开一个测试文件
                // 注意：实际使用时应使用一个真实存在的临时文件路径
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    UseShellExecute = true   // 必须设置为 true
                };
                Process.Start(startInfo);
                // 如果上面这行没有抛出异常，通常意味着存在关联程序
                // 但需要注意，即使有关联，也可能因其他原因启动失败
                return string.Empty;
            }
            catch (Exception ex)
            {
                // 如果抛出异常，则可能没有关联程序，或关联程序不可用
                // 这里可以处理“没有找到关联应用”的逻辑
                return $"打开文件败: {ex.Message}";
            }
        }
        private void ExecutePreviewMergedRpt(ObservableCollection<RptFileViewModel> rptFiles)
        {
            //System.Windows.MessageBox.Show($"预览合并报表: {rptFiles.Count}");
            var rpts = _rptFileManager.SwitchFiles.Values;            
            var previewFile = GenPreviewXlsFile();
            var errMsg = ExcelRptGenerator.GenerateReport(ExcelRptTemplate, previewFile, _rptFileManager.SwitchFiles);
            if (string.IsNullOrEmpty(errMsg) && _hasXlsAssociatedApp)
            {
                errMsg = TryOpenRptXls(previewFile);
                if (!string.IsNullOrEmpty(errMsg))
                {
                    System.Windows.MessageBox.Show(errMsg);
                }
            }
            else
            {
                System.Windows.MessageBox.Show($"合并报表失败: {errMsg}");
            }
        }

        string _tmpFold;
        string _previewFolder;
        string GenPrintXlsFile(long hisId)
        {
            if(string.IsNullOrEmpty(_tmpFold) || !Directory.Exists(_tmpFold))
            {
                _tmpFold = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "his");
                if (!Directory.Exists(_tmpFold)) Directory.CreateDirectory(_tmpFold);
            }
            return Path.Combine(_tmpFold, $"{hisId}.xlsx");
        }
        string GenPreviewXlsFile()
        {
            if (string.IsNullOrEmpty(_previewFolder) || !Directory.Exists(_previewFolder))
            {
                _previewFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "preview");
                if (!Directory.Exists(_previewFolder)) Directory.CreateDirectory(_previewFolder);
            }
            return Path.Combine(_previewFolder, $"{Guid.NewGuid().ToString()}.xlsx");
        }
        private void ExecuteOpenMergedRpt(ObservableCollection<RptHisViewModel> rptHis)
        {            
            if (SelectedRptHis.Count != 1) return;
            
            var excelBytes = _switchHisRepos.GetExcel(SelectedRptHis[0].Model.Id);
            if (excelBytes == null || excelBytes.Length == 0)
            {
                var rpts = Util.ParseRptHisJson(SelectedRptHis[0].Model.RptJson);
                var printFile = GenPrintXlsFile(SelectedRptHis[0].Model.Id);
                var errMsg = ExcelRptGenerator.GenerateReport(ExcelRptTemplate, printFile, rpts);
                if (!string.IsNullOrEmpty(errMsg))
                {
                    System.Windows.MessageBox.Show($"生成打表失败: {errMsg}");
                    return;
                }

                try
                {
                    _switchHisRepos.SetExcel(SelectedRptHis[0].Model.Id, File.ReadAllBytes(printFile));
                    Log.Information($"更新历史xls报表[{SelectedRptHis[0].Model.Id}]成功");
                }
                catch (Exception ex)
                {
                    Log.Error($"更新历史xls报表失败: {ex}");
                }

                if (!_hasXlsAssociatedApp)
                {
                    System.Windows.MessageBox.Show($"报表文件已生成，未发现关联程序打开它(是否安装了Office或者WPS): \r\n报表文件:{printFile}");
                    return;
                }

                errMsg = TryOpenRptXls(printFile);
                if (!string.IsNullOrEmpty(errMsg))
                {
                    System.Windows.MessageBox.Show(errMsg);
                    return;
                }
            }
            else
            {
                var printFile = GenPrintXlsFile(SelectedRptHis[0].Model.Id);
                if (!File.Exists(printFile))
                {
                    try
                    {
                        File.WriteAllBytes(printFile, excelBytes);
                    }
                    catch (Exception ex)
                    {
                        var msg = $"生成报表文件失败!\r\n文件:{printFile}\r\n原因:{ex.Message}";
                        Log.Error(msg);
                        System.Windows.MessageBox.Show(msg);
                        return;
                    }
                }
                
                if (!_hasXlsAssociatedApp)
                {
                    System.Windows.MessageBox.Show($"报表文件已生成，未发现关联程序打开它(是否安装了Office或者WPS): \r\n报表文件:{printFile}");
                    return;
                }
                var errMsg = TryOpenRptXls(printFile);
                if (!string.IsNullOrEmpty(errMsg))
                {
                    var processIds = FileLockHelper.GetLockingProcessIds(printFile);
                    if (processIds.Count == 0)
                    {
                        var msg = $"该文件未被任何进程占用，但是{errMsg}";
                        Log.Error(msg);
                        System.Windows.MessageBox.Show(msg);
                        return;
                    }
                    try
                    {
                        using (var proc = Process.GetProcessById((int)processIds[0]))
                        {
                            Log.Warning($"报表文件({printFile})被进程({proc.Id})占用，尝试激活它");
                            WindowActivator.ActivateWindow(proc);
                        }
                        
                    }
                    catch(Exception ex)
                    {
                        var msg = $"报表文件({printFile})未其他进程占用，未能操作该进程: {ex.Message}";
                        Log.Warning(msg);
                        System.Windows.MessageBox.Show(msg);
                    }                    
                    return;
                }
            }
        }
        private void ExecuteExportMergedRpt(ObservableCollection<RptHisViewModel> rptHis)
        {
            System.Windows.MessageBox.Show($"ExecuteOpenMergedRpt: {SelectedRptHis.Count}");
        }

        private void ExecuteRefreshRptCommand(ObservableCollection<RptFileViewModel> rptFiles)
        {
            RefreshSwitchRptFiles();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void ExecuteSelectAppLoc(AppViewModel model)
        {
            // 设置报表输出位置
            //System.Windows.MessageBox.Show($"设置{model.Name}报表文件夹");
            var app = model.Model;
            string folder;
            using var dlg = new FolderBrowserDialog();
            dlg.UseDescriptionForTitle = true;
            dlg.Description = $"选择{model.Name}报表文件夹";
            dlg.RootFolder = Environment.SpecialFolder.MyComputer;
            dlg.ShowNewFolderButton = false;
            if (dlg.ShowDialog() != DialogResult.OK) return;

            folder = dlg.SelectedPath;
            if (string.IsNullOrEmpty(folder)) return;
            // 不区分大小写比较，返回 bool
            if (string.Equals(app.RptFolder, folder, StringComparison.OrdinalIgnoreCase))
            {
                System.Windows.MessageBox.Show($"文件夹未改变");
                return;
            }
            
            //保存至内存
            app.RptFolder = folder;
            //保存至注册表
            Util.SaveAppPath(model.RptFolderRegKey, folder);

            //切换监控目录
            if (_rptWatchers.TryGetValue(app.Type, out var watcher))
            {
                try
                {
                    if (watcher.EnableRaisingEvents)
                    {
                        watcher.EnableRaisingEvents = false;
                    }

                    // 2. 更新监控路径
                    Log.Information($"Update rpt folder from {watcher.Path} to {folder}");
                    watcher.Path = folder;

                    // 3. 重新设置其他属性（如果需要）
                    //watcher.IncludeSubdirectories = true;
                    //watcher.Filter = "*.*";

                    // 4. 启动新监控
                    watcher.EnableRaisingEvents = true;
                }
                catch(Exception ex)
                {
                    Log.Error($"Remonitor rpt path failed: {ex}");
                }
            }
            else
            {
                //启动新的monitor,已存在的不会重新创建
                MonitorScanFolders(Apps);
            }
        }


        private void ExecuteDynamicAction(AppViewModel model)
        {
            var app = model.Model;
            if (app == null) return;
            //System.Windows.MessageBox.Show($"{app.ActionText} : {app.Name}");
            if (app.Process != null)
            {
                var close = model.ActionText.Contains("关闭");
                WindowActivator.ActivateWindow(app.Process, close);
                if (!close && TopMost) //最顶端时就最小化
                {
                    WindowActivator.MinimizeOrRestoreProcessWindow(Process.GetCurrentProcess());
                }
            }
            else if (!string.IsNullOrEmpty(app.FullName))
            {
                Util.StartApp(app.FullName);
            }
            else if (app.Type == AppType.HVC)
            {
                OnBrowseApp(model);
            }
        }

        void OnBrowseApp(AppViewModel model)
        {
            var app = model.Model;
            string folder = null;
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择程序的安装目录";
                dlg.UseDescriptionForTitle = true;
                dlg.RootFolder = Environment.SpecialFolder.MyComputer;
                dlg.ShowNewFolderButton = false;
                if (dlg.ShowDialog() == DialogResult.OK)
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
            MonitorProcess(model);
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke((T)parameter) ?? true;
        public void Execute(object parameter) => _execute((T)parameter);
        public event EventHandler CanExecuteChanged;
    }    

    
}

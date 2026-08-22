using ClosedXML;
using CtrlCenter.DataModel;
using CtrlCenter.Excel;
using CtrlCenter.Interfaces;
using CtrlCenter.Logic;
using CtrlCenter.Storage;
using DocumentFormat.OpenXml.EMMA;
using Serilog;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
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
        public bool TopMost => _appSetting.TopMost;
        public bool ShowRptName => false;

        public ObservableCollection<AppViewModel> Apps { get; set; } = new ObservableCollection<AppViewModel>();
        public ObservableCollection<RptFileViewModel> RptFiles { get; set; } = new ObservableCollection<RptFileViewModel>();
        public ObservableCollection<RptHisViewModel>  RptHis { get; set; } = new ObservableCollection<RptHisViewModel>();

        public ICommand EditAppNameCommand { get; }
        public ICommand SelectAppLocCommand { get; }
        public ICommand DynamicActionCommand { get; }
        public ICommand MergeRptCommand { get; }
        public ICommand PreviewMergedRptCommand { get; }
        public ICommand RefreshRptCommand { get; }
        public bool CanMergeRpt => RptFiles.Count >= 2;
        public bool CanPreviewMergedRpt => RptFiles.Count > 1;
        private AppModel[] InitApps()
        {
            var apps = new AppModel[]
            {
                new AppModel
                {
                        Type = AppType.ZKC,
                        Guid = "{B41B0EBC-95F3-45A5-AE4C-4A40696C198D}_is1",
                        Name = "ZKC1601开关机械特性综合测试系统",
                        Exe = "ZKC1601S",
                        GetTxtAndSwitchNo = Util.GetTxtAndNoFromZkc,
                        RptPattern = "????????????_*.rpt",
                 },
                 new AppModel
                    {
                        Type = AppType.LRT,
                        Guid = "{28692C18-A1DF-465B-9359-42C6F601243A}_is1",
                        Name = "三通道回路电阻测试仪后台软件",
                        Exe = "IRTest",
                        GetTxtAndSwitchNo = Util.GetTxtAndNoFromLrt,
                        RptPattern = "????????????_ir*.rpt",
                    },
                    new AppModel
                    {
                        Type = AppType.HVC,
                        Guid = string.Empty,
                        Name = "高压线缆测试系统",
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
                Log.Information($"Monitor directory: {app.RptFolder}");
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
            Log.Information($"检测到[{model.Name}]创建报表文件: {Path.GetFileName(e.FullPath)}");
        }

        private void OnFileChanged(AppViewModel model, FileSystemEventArgs e)
        {
            //文件修改时，Changed 事件可能会触发多次（因为写入过程中多次写磁盘），
            //建议在事件处理中加入防抖逻辑（比如延迟 500ms 再处理）。
            // 延迟一下，等待文件写入完成
            Log.Information($"检测到[{model.Name}]报表被修改: {Path.GetFileName(e.FullPath)}");
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

            EditAppNameCommand = new RelayCommand<AppViewModel>(ExecEditAddName);
            SelectAppLocCommand = new RelayCommand<AppViewModel>(ExecuteSelectAppLoc);
            DynamicActionCommand = new RelayCommand<AppViewModel>(ExecuteDynamicAction);

            MergeRptCommand = new RelayCommand<ObservableCollection<RptFileViewModel>>(ExecuteMergeRpt);
            PreviewMergedRptCommand = new RelayCommand<ObservableCollection<RptFileViewModel>>(ExecutePreviewMergedRpt);
            RefreshRptCommand = new RelayCommand<ObservableCollection<RptFileViewModel>>(ExecuteRefreshRptCommand);
        }
        private void ExecuteMergeRpt(ObservableCollection<RptFileViewModel> rptFiles)
        {
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

        private void ExecutePreviewMergedRpt(ObservableCollection<RptFileViewModel> rptFiles)
        {
            System.Windows.MessageBox.Show($"预览合并报表: {rptFiles.Count}");
            var rpts = _rptFileManager.SwitchFiles.Values;
            var rptTemplate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rpt_template.xlsx");
            ExcelRptGenerator.GenerateReport(rptTemplate, "rpt.xlsx", _rptFileManager.SwitchFiles);
            //var switchNo = _rptFileManager.SwitchFiles.Values.FirstOrDefault().SwitchNo;
            //var switchReport = Util.BuildSwitchHisEntity(_rptFileManager.SwitchFiles, switchNo);
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

        private void ExecEditAddName(AppViewModel data)
        {
            // 编辑逻辑
            System.Windows.MessageBox.Show($"编辑: {data.Name}");
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
                if (!close)
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

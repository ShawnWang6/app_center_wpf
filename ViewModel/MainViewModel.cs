using CtrlCenter.DataModel;
using CtrlCenter.Interfaces;
using CtrlCenter.Logic;
using CtrlCenter.Storage;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ClosedXML;
using CtrlCenter.Excel;

namespace CtrlCenter.ViewModel
{
    public class MainViewModel : INotifyPropertyChanged
    {        
        private readonly ISwitchHisRepos _switchHisRepos;
        private readonly AppModel[] _appModels;
        private readonly RptFileManager _rptFileManager = new RptFileManager();
        private readonly HashSet<AppViewModel> _runningApp = new HashSet<AppViewModel>();        
        private readonly List<FileSystemWatcher> _rptWatchers = new();
        private readonly ManagementEventWatcher _appWatcher;        
        private readonly RptHisManager _rptHisManager;

        public ObservableCollection<AppViewModel> Apps { get; set; } = new ObservableCollection<AppViewModel>();
        public ObservableCollection<RptFileViewModel> RptFiles { get; set; } = new ObservableCollection<RptFileViewModel>();
        public ObservableCollection<RptHisViewModel>  RptHis { get; set; } = new ObservableCollection<RptHisViewModel>();

        public ICommand EditAppNameCommand { get; }
        public ICommand SelectAppLocCommand { get; }
        public ICommand DynamicActionCommand { get; }
        public ICommand MergeRptCommand { get; }
        public ICommand PreviewMergedRptCommand { get; }
        public bool CanMergeRpt => RptFiles.Count > 2;
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
            }
        }
        void RefreshSwitchRptFiles()
        {
            RptFiles.Clear();
            foreach (var file in _rptFileManager.SwitchFiles.Values)
            {
                RptFiles.Add(new RptFileViewModel(file));
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
                if (string.IsNullOrEmpty(app.ScanFolder)) continue;

                Debug.WriteLine($"Monitoring directory: {app.ScanFolder}");
                var watcher = new FileSystemWatcher(app.ScanFolder)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    Filter = app.RptPattern.Remove(0, app.RptPattern.Length - 5),
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
                _rptWatchers.Add(watcher); // Keep a reference to prevent garbage collection
            }
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
                Debug.WriteLine($"[OnProcessExited线程:{Thread.CurrentThread.ManagedThreadId,2}] [{app.Model.Exe}] 已经退出");
            }));
        }

        public MainViewModel(ISwitchHisRepos switchHisRepos)
        {
            _switchHisRepos = switchHisRepos;
            _rptHisManager = new RptHisManager(_switchHisRepos);
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
        }
        private void ExecuteMergeRpt(ObservableCollection<RptFileViewModel> rptFiles)
        {
            var (ok, err) = _rptHisManager.SaveRptfiles(_rptFileManager.SwitchFiles);
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

        private void ExecuteSelectAppLoc(AppViewModel data)
        {
            // 删除逻辑
            System.Windows.MessageBox.Show($"删除: {data.Name}");
        }

        private void ExecuteDynamicAction(AppViewModel model)
        {
            var app = model.Model;
            if (app == null) return;
            System.Windows.MessageBox.Show($"{app.ActionText} : {app.Name}");
            if (app.Process != null)
            {
                WindowActivator.ActivateWindow(app.Process, true);
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

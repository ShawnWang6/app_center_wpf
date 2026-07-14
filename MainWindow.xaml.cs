using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Windows;
using Newtonsoft.Json;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Media;

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
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _apps = new[]
            {
                    new AppInfo
                    {
                        Guid = "{B41B0EBC-95F3-45A5-AE4C-4A40696C198D}_is1",
                        Name = "ZKC1601S开关机械特性综合测试系统",
                        Exe = "ZKC1601S",
                        DevNoGet = AppInfo.GetDevNoFromSwitch,
                    },
                    new AppInfo
                    {
                        Guid = "{28692C18-A1DF-465B-9359-42C6F601243A}_is1",
                        Name = "三通道回路电阻测试仪后台软件",
                        Exe = "IRTest",
                        DevNoGet = AppInfo.GetDevNoFromIr,
                    },
                    new AppInfo
                    {
                        Guid = string.Empty,
                        Name = "高压线缆测试系统",
                        Exe = "HighVoltCableTestSystem",
                        DevNoGet = AppInfo.GetDevNoFromHvc,
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
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var parts = fileName.Split('_');            
            var timestamp = parts[0];
            string switchNo = null;
            if (filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                switchNo = app.DevNoGet(filePath);
            }
            else if (filePath.EndsWith(".rpt", StringComparison.OrdinalIgnoreCase))
            {
                switchNo = app.DevNoGet(filePath);
            }
            else
            {
                Debug.WriteLine($"线程[{Thread.CurrentThread.ManagedThreadId}] 未能识{app.Name}报表文件: {filePath}");
                return;
            }
            if (string.IsNullOrEmpty(switchNo))
            {
                Debug.WriteLine($"线程[{Thread.CurrentThread.ManagedThreadId}] 未能识{app.Name}报表文件开关: {filePath}");
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateExperimentOutput(app, switchNo, timestamp);
            }));
        }

        private void UpdateExperimentOutput(AppInfo app, string switchNo, string timestamp)
        {
            listViewExperimentOutput.Items.Clear();
            var masterOutput = new
            {
                AppName = app.Name,
                SwitchNo = switchNo,
                ExperimentTime = timestamp
            };
            listViewExperimentOutput.Items.Add(masterOutput);

            // Check other apps for matching switch numbers
            foreach (var otherApp in _apps.Where(a => a != app))
            {
                if (string.IsNullOrEmpty(otherApp.ScanFolder) || !Directory.Exists(otherApp.ScanFolder))
                {
                    Debug.WriteLine($"ScanFolder does not exist for app: {otherApp.Name}");
                    continue;
                }

                var latestFile = Directory.GetFiles(otherApp.ScanFolder, "*.*")
                    .Select(f => new { File = f, Time = File.GetLastWriteTime(f) })
                    .OrderByDescending(f => f.Time)
                    .FirstOrDefault();

                if (latestFile != null)
                {
                    var otherFileName = Path.GetFileNameWithoutExtension(latestFile.File);
                    var othewrSwitchNo = otherApp.DevNoGet(latestFile.File);
                    if (othewrSwitchNo != switchNo) continue;
                    var slaveOutput = new
                    {
                        AppName = otherApp.Name,
                        SwitchNo = switchNo,
                        ExperimentTime = timestamp
                    };
                    listViewExperimentOutput.Items.Add(slaveOutput);
                }
            }
        }


        void UpdateAppActionCell(AppInfo app)
        {
            app.NotifyActionChanged();
        }

        bool StartApp(AppInfo app)
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(app.FullName),
                FileName = app.FullName,
                Verb = "runas"
            };

            try
            {
                using (Process.Start(startInfo)) { }
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"启动失败: {ex.Message}");
            }

            return false;
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
                StartApp(app);
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
        public string Guid { get; set; }
        public string Name { get; set; }
        public string Exe { get; set; }
        public string Desc { get; set; }

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

        public static string GetDevNoFromSwitch(string filePath)
        {
            var json = File.ReadAllText(filePath);
            dynamic data = JsonConvert.DeserializeObject(json);
            return data?.RptCfg?.SwitchNo;
        }
        public static string GetDevNoFromIr(string filePath)
        {
            var json = File.ReadAllText(filePath);
            dynamic data = JsonConvert.DeserializeObject(json);
            return data?.DevId;
        }

        public static string GetDevNoFromHvc(string filePath)
        {
            // Handle CSV file (app3)
            var lines = File.ReadAllLines(filePath, System.Text.Encoding.ASCII);
            if (lines.Length > 0)
            {
                return lines[0].Split(',')[0]; // First column is the switch number
            }
            return null;
        }
        public Func<string, string> DevNoGet;
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
}

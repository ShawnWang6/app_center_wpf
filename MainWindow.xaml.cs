using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Windows;
using System.Windows.Controls;
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
                },
                new AppInfo
                {
                    Guid = "{28692C18-A1DF-465B-9359-42C6F601243A}_is1",
                    Name = "三通道回路电阻测试仪后台软件",
                    Exe = "IRTest",
                },
                new AppInfo
                {
                    Guid = string.Empty,
                    Name = "高压线缆测试系统",
                    Exe = "HighVoltCableTestSystem",
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
                UpdateAppActionCell(app);
            }

            dataGridApps.ItemsSource = _apps;
            _appWatcher = MonitorApps(_apps);
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

        public Brush ActionForeground
        {
            get
            {
                if (string.IsNullOrEmpty(FullName))
                    return Brushes.Black;
                return Process == null ? Brushes.Green : Brushes.Red;
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        public void NotifyActionChanged()
        {
            OnPropertyChanged(nameof(ActionText));
            OnPropertyChanged(nameof(ActionForeground));
        }
    }
}

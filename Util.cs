using CtrlCenter.AppRptModel;
using CtrlCenter.DataModel;
using CtrlCenter.Storage;
using DocumentFormat.OpenXml.Drawing.Diagrams;
using DocumentFormat.OpenXml.Office2013.Excel;
using Microsoft.Win32;
using Newtonsoft.Json;
using Serilog;
using System.ComponentModel;
//using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

namespace CtrlCenter
{
    public static class Util
    {
        public static readonly Encoding GbkEncoding = Encoding.GetEncoding("GBK");

        public static (string, string) GetTxtAndNoFromZkc(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var data = JsonConvert.DeserializeObject<ZkcRptSwitchNoModel>(json);
            return (json, data?.RptCfg?.SwitchNo);
        }
        public static (string, string) GetTxtAndNoFromLrt(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var data = JsonConvert.DeserializeObject<LrtRptSwitchNoModel>(json);
            return (json, data?.DevId);
        }

        public static (string, string) GetTxtAndNoFromHvc(string filePath)
        {
            // Handle CSV file (app3)      
            var lines = File.ReadAllLines(filePath, GbkEncoding);
            if (lines.Length > 0)
            {
                return (lines[0], lines[0].Split(',')[0]); // First column is the switch number
            }
            return (null, null);
        }

        public static SwitchHisEntity BuildSwitchHisEntity(IDictionary<AppType, RptFile> rpts, string switchNo)
        {
            var (minTime, maxTime) = rpts.Values.Aggregate(
                (Min: long.MaxValue, Max: long.MinValue),
                (acc, f) => (
                Min: f.TimeStamp < acc.Min ? f.TimeStamp : acc.Min,
                Max: f.TimeStamp > acc.Max ? f.TimeStamp : acc.Max
                )
            );
            return new SwitchHisEntity
            {
                SwitchNo = switchNo,
                MinTime = ParseYyMmDdHhMmSs(minTime),
                MaxTime = ParseYyMmDdHhMmSs(maxTime),
                RptJson = JsonConvert.SerializeObject(rpts)
            };
        }

        public static void SimLrtReport(string folder, string switchNo)
        {
            var now = DateTime.Now;
            var testTime = now.ToString("yyyy-MM-dd HH:mm:ss");
            var model = new LrtRptModel
            {
                Beizhu = "0916",
                DevId = switchNo,
                I = "12.0A",
                IOk = "不合格",
                LogTime = testTime,
                Model = "",
                Project = "",
                Ra = "11.00μΩ",
                RaOk = "合格",
                RangeI = "[1, 11]",
                RangeR = "[2, 22]",
                Rb = "11.00μΩ",
                RbOk = "合格",
                Rc = "11.00μΩ",
                RcOk = "合格",
                Temp = "3.7℃",
                TestTime = testTime,
                Tester = ""
            };
            var json = JsonConvert.SerializeObject(model);
            File.WriteAllText(Path.Combine(folder, $"{now.ToString("yyMMddHHmmss")}_ir_{switchNo}.rpt"), json);
        }
        public static void SimHvcReport(string folder, string switchNo)
        {
            var now = DateTime.Now;
            var testTime = now.ToString("yyyy-MM-dd HH:mm:ss");
            //8888,112,0.001 mA,0.052 mA,14.8 GΩ,OK
            var model = new HvcRptModel
            {
                TestTime = testTime,
                SwitchNo = switchNo,
                Dc = "0.001 mA",
                Ac = "0.052 mA",
                InsRes = "14.8 GΩ",
                Result = "OK"
            };
            var line = $"{model.SwitchNo},{model.SwitchNo},{model.Dc},{model.Ac},{model.InsRes},{model.Result}";            
            File.WriteAllText(Path.Combine(folder, $"{now.ToString("yyMMddHHmmss")}.csv"), line, GbkEncoding);
        }

        public static void SimZkcReport(string folder, string switchNo)
        {
            var now = DateTime.Now;
            var testTime = now.ToString("yyyy-MM-dd HH:mm:ss");            
            var templJson = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zkc_rpt_template.rpt"));
            var model = JsonConvert.DeserializeObject<ZkcAtRptModel>(templJson);
            model.TestTime = testTime;
            model.RptCfg.SwitchNo = switchNo;
            var json = JsonConvert.SerializeObject(model);
            File.WriteAllText(Path.Combine(folder, $"{now.ToString("yyMMddHHmmss")}_zkc.rpt"), json);
        }

        public static T BuildExcelRptModel<T>(RptFile file) where T : class
        {
            T result = default;
            if (file.FileType == AppType.HVC)
            {
                var tokens = file.Content.Split(',');
                result = new HvcRptModel
                {
                    TestTime = ParseYyMmDdHhMmSs(file.TimeStamp).ToLongDateString(),
                    SwitchNo = tokens.Length > 1 ? tokens[1] : string.Empty,                    
                    Dc = tokens.Length > 2 ?  tokens[2] : string.Empty,
                    Ac = tokens.Length > 3 ? tokens[3] : string.Empty,
                    InsRes = tokens.Length > 4 ? tokens[4] : string.Empty,
                    Result = tokens.Length > 5 ? tokens[5] : string.Empty,
                } as T;
            }
            else
            {
                result = JsonConvert.DeserializeObject<T>(file.Content);
            }
            return result;
        }

        public static bool StartApp(string appFullName)
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(appFullName),
                FileName = appFullName,
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
        public static long GetNowRptTimestamp()
        {
            return long.Parse(DateTime.Now.ToString("yyMMddHHmmss"));
        }
        public static DateTime ParseYyMmDdHhMmSs(long input)
        { 
            DateTime result = DateTime.Now;
            if (DateTime.TryParseExact($"{input}", "yyMMddHHmmss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            {
                Console.WriteLine($"解析成功: {result}");
            }
            return result;
        }

        public static IEnumerable<string> FindFilesEnumerable(string rootPath, string searchPattern)
        {
            var queue = new Queue<string>();
            queue.Enqueue(rootPath);

            while (queue.Count > 0)
            {
                string currentDir = queue.Dequeue();
                string[] subDirs;

                try
                {
                    subDirs = Directory.GetDirectories(currentDir);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (string subDir in subDirs)
                {
                    queue.Enqueue(subDir);
                }

                IEnumerable<string> files = null;
                try
                {
                    files = Directory.EnumerateFiles(currentDir, searchPattern);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                if (files != null)
                {
                    foreach (string file in files)
                    {
                        yield return file;
                    }
                }
            }
        }

        public static RegistryView GetTargetView()
        {
            return Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32;
        }

        public static void SaveAppPath(string name, string path)
        {
            RegistryView targetView = GetTargetView();
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, targetView))
            {
                using (RegistryKey subKey = baseKey.CreateSubKey(AppRegKey))
                {
                    subKey?.SetValue(name, path, RegistryValueKind.String);
                }
            }
        }

        public static string LoadAppPath(string name)
        {
            string path = string.Empty;
            RegistryView targetView = GetTargetView();
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, targetView))
            {
                using (RegistryKey subKey = baseKey.OpenSubKey(AppRegKey))
                {
                    return subKey?.GetValue(name) as string;
                }
            }
        }
        public static readonly string AppRegKey = @"SOFTWARE\ZhengGuan\ReportMaker";
        public static int AppRegGetInt(string key, int valueDefault)
        {
            string path = string.Empty;
            RegistryView targetView = GetTargetView();
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, targetView);
            using RegistryKey subKey = baseKey.OpenSubKey(AppRegKey);
            var value = subKey.GetValue(key);
            if (subKey == null) { return valueDefault; }            
            int result = valueDefault;
            if (int.TryParse($"{value}", out result))
            {
                //TODO log more
            }
            return result;
        }
        public static bool AppRegSetInt(string name, int value)
        {
            RegistryView targetView = GetTargetView();
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, targetView);
            using RegistryKey subKey = baseKey.CreateSubKey(AppRegKey);
            if (subKey == null) { return false; }
            subKey.SetValue(name, value, RegistryValueKind.DWord);
            return true;
        }
        // 泛型读取方法
        public static T AppRegGet<T>(string key, T valueDefault)
        {
            RegistryView targetView = GetTargetView();
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, targetView);
            using RegistryKey subKey = baseKey.CreateSubKey(AppRegKey);
            if (subKey == null) { return valueDefault; }
            object rawValue = subKey.GetValue(key);
            if (rawValue == null) { return valueDefault; }
            return ConvertValue<T>(rawValue, valueDefault);
        }

        // 泛型写入方法
        public static bool AppRegSet<T>(string name, T value)
        {
            RegistryView targetView = GetTargetView();
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, targetView);
            using RegistryKey subKey = baseKey.CreateSubKey(AppRegKey);
            if (subKey == null) { return false; }

            RegistryValueKind kind = GetRegistryValueKind(value);
            subKey.SetValue(name, value, kind);
            return true;
        }

        // 类型转换辅助方法
        private static T ConvertValueOld<T>(object rawValue, T defaultValue)
        {
            Type targetType = typeof(T);

            if (targetType == typeof(int))
            {
                if (rawValue is int intValue)
                    return (T)(object)intValue;
                if (int.TryParse(rawValue.ToString(), out int result))
                    return (T)(object)result;
            }
            else if (targetType == typeof(long))
            {
                if (rawValue is long longValue)
                    return (T)(object)longValue;
                if (rawValue is int intValue)
                    return (T)(object)(long)intValue;
                if (long.TryParse(rawValue.ToString(), out long result))
                    return (T)(object)result;
            }
            else if (targetType == typeof(string))
            {
                return (T)(object)rawValue.ToString();
            }

            // 不支持的类型或转换失败，返回默认值
            return defaultValue;
        }

        // 获取 RegistryValueKind
        private static RegistryValueKind GetRegistryValueKind<T>(T value)
        {
            Type type = typeof(T);

            if (type == typeof(int))
                return RegistryValueKind.DWord;
            else if (type == typeof(long))
                return RegistryValueKind.QWord;
            else if (type == typeof(string))
                return RegistryValueKind.String;
            else
                throw new NotSupportedException($"Type {type.Name} is not supported");
        }
        private static T ConvertValue<T>(object rawValue, T defaultValue)
        {
            try
            {
                return (T)Convert.ChangeType(rawValue, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        public static Process GetProcess(EventArrivedEventArgs e)
        {
            try
            {
                uint processId = (uint)e.NewEvent.Properties["ProcessID"].Value;
                var proc = Process.GetProcessById((int)processId);
                return proc;
            }
            catch
            {
                return null;
            }
        }

        public static bool IsProcessRunning(string path)
        {
            string targetPath = Path.GetFullPath(path).ToLowerInvariant();
            return Process.GetProcesses().Where(p =>
            {
                try
                {
                    string processPath = p.MainModule?.FileName;
                    if (string.IsNullOrEmpty(processPath)) return false;

                    processPath = Path.GetFullPath(processPath).ToLowerInvariant();
                    return processPath == targetPath;
                }
                catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
                {
                    return false;
                }
            }).Any();
        }

        public static string GetProcessPathByWMI(int processId)
        {
            try
            {
                string query = $"SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = {processId}";

                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                using (ManagementObjectCollection processes = searcher.Get())
                {
                    foreach (ManagementObject process in processes)
                    {
                        string path = process["ExecutablePath"]?.ToString();
                        if (!string.IsNullOrEmpty(path))
                            return path;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"WMI查询进程 {processId} 路径失败: {ex.Message}");
            }

            return null;
        }

        public static Process GetProcess(string path, bool excludeSelf = false)
        {
            var process_name = Path.GetFileNameWithoutExtension(path);

            int selfProId = -1;
            using (var self = Process.GetCurrentProcess())
            {
                selfProId = self.Id;
            }

            string targetPath = Path.GetFullPath(path).ToLowerInvariant();
            DateTime from = DateTime.Now;
            var processes = Process.GetProcesses();
            Log.Debug($"GetProcesses consum {(DateTime.Now - from).TotalSeconds}");
            var ret = Process.GetProcesses().FirstOrDefault(p =>
            {
                try
                {
                    if (selfProId == p.Id)
                    {
                        return false;
                    }
                    if (!process_name.Equals(p.ProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                    string processPath = p.MainModule?.FileName;
                    if (string.IsNullOrEmpty(processPath)) return false;

                    processPath = Path.GetFullPath(processPath).ToLowerInvariant();
                    return processPath == targetPath;
                }
                catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
                {
                    return false;
                }
            });
            foreach (var p in processes)
            {
                if (!p.Equals(ret)) p.Dispose();
            }
            return ret;
        }

        public static bool TryGetInstallLocationByGuid(string guid, out string installLocation)
        {
            installLocation = null;
            if (!guid.StartsWith("{"))
                guid = "{" + guid + "}";

            string[] uninstallRoots = new[]
            {
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",                
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (string root in uninstallRoots)
            {
                using (var key = Registry.LocalMachine.OpenSubKey(root))
                {
                    if (key != null)
                    {
                        using (var subKey = key.OpenSubKey(guid))
                        {
                            if (subKey != null)
                            {
                                installLocation = subKey.GetValue("InstallLocation") as string;
                                if (string.IsNullOrEmpty(installLocation))
                                {
                                    string displayIcon = subKey.GetValue("DisplayIcon") as string;
                                    if (!string.IsNullOrEmpty(displayIcon))
                                        installLocation = Path.GetDirectoryName(displayIcon);
                                }
                                return true;
                            }
                        }
                    }
                }
            }

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
            {
                if (key != null)
                {
                    using (RegistryKey subKey = key.OpenSubKey(guid))
                    {
                        if (subKey != null)
                        {
                            installLocation = subKey.GetValue("InstallLocation") as string;
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }

    public static class WindowActivator
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetLastActivePopup(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const int SW_SHOWMAXIMIZED = 3;
        private const int SW_SHOWMINIMIZED = 2;

        public static bool ActivateWindow(Process process, bool close = false)
        {
            if (process == null) return false;

            IntPtr hWnd = process.MainWindowHandle;
            if (hWnd == IntPtr.Zero)
            {
                hWnd = FindWindowByProcessId(process.Id);
            }

            if (hWnd != IntPtr.Zero)
            {
                if (IsZoomed(hWnd))
                {
                    ShowWindow(hWnd, SW_SHOWMAXIMIZED);
                }
                if (ForceForegroundWindow(hWnd))
                {
                    if (close)
                    {
                        var popWnd = GetLastActivePopup(hWnd);
                        Log.Debug($"活动窗口是主窗口: {(popWnd == hWnd)}");
                        if (popWnd == hWnd)
                        {
                            PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                        }
                    }
                    return true;
                }
            }

            return false;
        }

        public static bool MinimizeOrRestoreProcessWindow(Process process)
        {
            if (process == null || process.MainWindowHandle == IntPtr.Zero)
            {
                Log.Debug("指定的进程无效或没有主窗口。");
                return false;
            }

            IntPtr hWnd = process.MainWindowHandle;

            // 检查窗口是否最小化
            if (IsIconic(hWnd))
            {
                // 如果最小化，则恢复窗口
                ShowWindow(hWnd, 9); // SW_RESTORE
                Log.Debug($"恢复了进程 {process.ProcessName} 的主窗口。");
            }
            else
            {
                // 如果未最小化，则最小化窗口
                ShowWindow(hWnd, 6); // SW_MINIMIZE
                Log.Debug($"最小化了进程 {process.ProcessName} 的主窗口。");
            }
            return true;
        }
        public static IntPtr GetFocusedWindowOfProcess(Process process)
        {
            if (process == null) throw new ArgumentNullException(nameof(process));

            IntPtr foregroundHwnd = GetForegroundWindow();
            if (foregroundHwnd == IntPtr.Zero)
                return IntPtr.Zero;

            uint foregroundProcessId;
            GetWindowThreadProcessId(foregroundHwnd, out foregroundProcessId);

            if (foregroundProcessId == process.Id)
                return foregroundHwnd;
            else
                return IntPtr.Zero;
        }

        private static bool ForceForegroundWindow(IntPtr hWnd)
        {
            if (IsIconic(hWnd))
            {
                ShowWindow(hWnd, SW_RESTORE);
            }
            var popWnd = GetLastActivePopup(hWnd);
            if (IntPtr.Zero != popWnd && popWnd != hWnd)
            {
                SetForegroundWindow(popWnd);
                ShowWindow(hWnd, SW_SHOW);
                return true;
            }

            uint foreThreadId = GetWindowThreadProcessId(GetForegroundWindow(), out _);
            uint appThreadId = GetCurrentThreadId();
            uint targetThreadId = GetWindowThreadProcessId(hWnd, out _);

            if (foreThreadId != appThreadId)
            {
                AttachThreadInput(foreThreadId, appThreadId, true);
                AttachThreadInput(targetThreadId, appThreadId, true);
                SetForegroundWindow(hWnd);
                AttachThreadInput(foreThreadId, appThreadId, false);
                AttachThreadInput(targetThreadId, appThreadId, false);
            }
            else
            {
                SetForegroundWindow(hWnd);
            }

            ShowWindow(hWnd, SW_SHOW);
            return true;
        }

        public static bool CloseMainWindow(Process process)
        {
            if (process == null)
                throw new ArgumentNullException(nameof(process));

            IntPtr hWnd = process.MainWindowHandle;
            if (hWnd == IntPtr.Zero)
            {
                return false;
            }

            return PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private static IntPtr FindWindowByProcessId(int processId)
        {
            IntPtr found = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                GetWindowThreadProcessId(hWnd, out uint windowProcessId);

                if (windowProcessId == processId && IsMainWindow(hWnd))
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }

        private static bool IsMainWindow(IntPtr hWnd)
        {
            return IsWindowVisible(hWnd) && GetWindowTextLength(hWnd) > 0;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public const uint WM_CLOSE = 0x0010;
    }
}

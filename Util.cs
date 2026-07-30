using CtrlCenter.AppRptModel;
using CtrlCenter.DataModel;
using CtrlCenter.Storage;
using DocumentFormat.OpenXml.Drawing.Diagrams;
using Microsoft.Win32;
using Newtonsoft.Json;
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
                using (RegistryKey subKey = baseKey.CreateSubKey(@"SOFTWARE\ZGKJ\AppCenter"))
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
                using (RegistryKey subKey = baseKey.OpenSubKey(@"SOFTWARE\ZGKJ\AppCenter"))
                {
                    return subKey?.GetValue(name) as string;
                }
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
                Debug.WriteLine($"WMI查询进程 {processId} 路径失败: {ex.Message}");
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
            Debug.WriteLine($"GetProcesses consum {(DateTime.Now - from).TotalSeconds}");
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
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
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
                if (ForceForegroundWindow(hWnd))
                {
                    if (close)
                    {
                        var popWnd = GetLastActivePopup(hWnd);
                        Debug.WriteLine($"活动窗口是主窗口: {(popWnd == hWnd)}");
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

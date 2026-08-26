using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CtrlCenter.Tools
{
    public static class FileLockHelper
    {
        // 引入 Windows API 函数
        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmEndSession(uint pSessionHandle);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(uint pSessionHandle, uint nFiles, string[] rgsFilenames,
            uint nApplications, [In] RM_UNIQUE_PROCESS[]? rgApplications, uint nServices, string[]? rgsServiceNames);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmGetList(uint pSessionHandle, out uint pnProcInfoNeeded, ref uint pnProcInfo,
            [In, Out] RM_PROCESS_INFO[]? rgAffectedApps, ref uint lpdwRebootReasons);

        // 定义需要的结构体
        [StructLayout(LayoutKind.Sequential)]
        private struct RM_UNIQUE_PROCESS
        {
            public int dwProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strAppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string strServiceShortName;
            public uint ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bRestartable;
        }

        // 对外公开的方法：传入文件路径，返回占用该文件的进程名列表
        public static List<string> GetProcessesLockingFile(string filePath)
        {
            var lockingProcesses = new List<string>();
            uint sessionHandle = 0;

            try
            {
                // 1. 启动 Restart Manager 会话
                string sessionKey = Guid.NewGuid().ToString();
                int result = RmStartSession(out sessionHandle, 0, sessionKey);
                if (result != 0) return lockingProcesses;

                // 2. 注册被占用的文件
                string[] resources = new string[] { filePath };
                result = RmRegisterResources(sessionHandle, (uint)resources.Length, resources, 0, null, 0, null);
                if (result != 0) return lockingProcesses;

                // 3. 查询占用文件的进程信息
                uint pnProcInfoNeeded = 0;
                uint pnProcInfo = 0;
                uint rebootReasons = 0;

                // 第一次调用，获取需要的缓冲区大小
                result = RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, null, ref rebootReasons);
                if (result != 0 || pnProcInfoNeeded == 0) return lockingProcesses;

                // 分配缓冲区并第二次调用，获取实际数据
                RM_PROCESS_INFO[] processInfo = new RM_PROCESS_INFO[pnProcInfoNeeded];
                pnProcInfo = pnProcInfoNeeded;
                result = RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, processInfo, ref rebootReasons);
                if (result != 0) return lockingProcesses;

                // 4. 解析结果，获取进程名
                for (int i = 0; i < pnProcInfo; i++)
                {
                    try
                    {
                        int processId = processInfo[i].Process.dwProcessId;
                        var process = Process.GetProcessById(processId);
                        lockingProcesses.Add(process.ProcessName + $" (PID: {processId})");
                    }
                    catch (ArgumentException)
                    {
                        // 进程可能在查询期间已退出
                        continue;
                    }
                }
            }
            finally
            {
                // 5. 始终记得结束会话
                if (sessionHandle != 0)
                    RmEndSession(sessionHandle);
            }

            return lockingProcesses;
        }

        // 新方法：返回占用文件的进程 ID 列表
        public static List<int> GetLockingProcessIds(string filePath)
        {
            var processIds = new List<int>();
            uint sessionHandle = 0;

            try
            {
                string sessionKey = Guid.NewGuid().ToString();
                int result = RmStartSession(out sessionHandle, 0, sessionKey);
                if (result != 0) return processIds;

                string[] resources = new string[] { filePath };
                result = RmRegisterResources(sessionHandle, (uint)resources.Length, resources, 0, null, 0, null);
                if (result != 0) return processIds;

                uint pnProcInfoNeeded = 0;
                uint pnProcInfo = 0;
                uint rebootReasons = 0;
                result = RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, null, ref rebootReasons);
                if (result != 0 || pnProcInfoNeeded == 0) return processIds;

                RM_PROCESS_INFO[] processInfo = new RM_PROCESS_INFO[pnProcInfoNeeded];
                pnProcInfo = pnProcInfoNeeded;
                result = RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, processInfo, ref rebootReasons);
                if (result != 0) return processIds;

                for (int i = 0; i < pnProcInfo; i++)
                {
                    processIds.Add(processInfo[i].Process.dwProcessId);
                }
            }
            finally
            {
                if (sessionHandle != 0)
                    RmEndSession(sessionHandle);
            }

            return processIds;
        }
    }
}
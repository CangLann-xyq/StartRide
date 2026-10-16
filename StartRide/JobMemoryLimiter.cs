using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StartRide.Core
{

    public static class JobMemoryLimiter
    {
        private const uint JOB_OBJECT_LIMIT_PROCESS_MEMORY = 0x00000100;
        private const uint JOB_OBJECT_LIMIT_JOB_MEMORY = 0x00000200;
        private const int JobObjectExtendedLimitInformation = 9;

        public sealed class AppliedLimit : IDisposable
        {
            internal AppliedLimit(IntPtr handle, int limitMb)
            {
                JobHandle = handle;
                LimitMb = limitMb;
            }

            public IntPtr JobHandle { get; }

            public int LimitMb { get; }

            public void Dispose()
            {
                if (JobHandle != IntPtr.Zero)
                {
                    try { CloseHandle(JobHandle); } catch { }
                }
            }
        }

        /// <summary>本机物理内存（MB）。取不到返回 0。</summary>
        public static int GetTotalPhysicalMemoryMb()
        {
            try
            {
                var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                if (GlobalMemoryStatusEx(ref status))
                    return (int)(status.ullTotalPhys / 1024UL / 1024UL);
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// 把「玩家设置的内存上限」纠正到一个**不会让 BeamNG 崩溃**的值。
        ///
        /// 为什么必须有这一层：这项设置是用 Windows 作业对象做**硬拒绝**的
        /// （游戏要的提交内存超限就被系统驳回，mimalloc 拿不到就 OOM 自杀）。
        /// 实测上限 4096 MB 时，BeamNG 0.39 加载官方地图会在十几秒后报
        /// `mimalloc: cannot commit OS memory (error: 1455)`，或 D3D12 分配失败
        /// 直接 `Fatal-ISV - The GPU crashed` 退出 —— 玩家看到的现象就是
        /// 「联机进去一会儿就闪退 / 各种玩法都看不到别人」。
        ///
        /// 下限取物理内存的一半，夹在 [6144, 32768]：
        ///   8 GB -> 6144 MB；16 GB -> 8192 MB；32 GB 及以上 -> 16384 MB。
        /// 设得比下限低就按安全值生效（调用方负责记日志），高于下限则**原样尊重玩家**。
        /// </summary>
        public static int ResolveSafeLimitMb(int requestedMb)
        {
            if (requestedMb <= 0) return requestedMb;

            int totalMb = GetTotalPhysicalMemoryMb();
            int safeFloor = 6144;
            if (totalMb > 0)
                safeFloor = Math.Max(safeFloor, Math.Min(totalMb / 2, 32768));

            return requestedMb < safeFloor ? safeFloor : requestedMb;
        }

        public static AppliedLimit? Apply(Process process, int limitMb)
        {
            if (process == null || limitMb <= 0) return null;

            // 保命钳制：设置里（或云端同步下来的）过低上限会让游戏进图时 OOM 自杀。
            limitMb = ResolveSafeLimitMb(limitMb);

            IntPtr job = IntPtr.Zero;
            try
            {
                job = CreateJobObject(IntPtr.Zero, null);
                if (job == IntPtr.Zero) return null;

                ulong bytes = (ulong)limitMb * 1024UL * 1024UL;
                var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                    {
                        LimitFlags = JOB_OBJECT_LIMIT_PROCESS_MEMORY | JOB_OBJECT_LIMIT_JOB_MEMORY,
                    },
                    ProcessMemoryLimit = (UIntPtr)bytes,
                    JobMemoryLimit = (UIntPtr)bytes,
                };

                int length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
                IntPtr buffer = Marshal.AllocHGlobal(length);
                try
                {
                    Marshal.StructureToPtr(info, buffer, false);
                    if (!SetInformationJobObject(job, JobObjectExtendedLimitInformation, buffer, (uint)length))
                    {
                        CloseHandle(job);
                        return null;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }

                if (!AssignProcessToJobObject(job, process.Handle))
                {
                    CloseHandle(job);
                    return null;
                }

                return new AppliedLimit(job, limitMb);
            }
            catch
            {
                if (job != IntPtr.Zero) CloseHandle(job);
                return null;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(
            IntPtr hJob, int infoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }
    }
}

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StartRide.Core
{
    /// <summary>
    /// 用 Windows 作业对象（Job Object）给游戏进程设「真实」的内存上限。
    ///
    /// 为什么用作业对象：BeamNG 没有命令行参数可以调内存，只把数字记在设置里等于没生效。
    /// 把游戏进程加入一个设了 JOB_OBJECT_LIMIT_PROCESS_MEMORY / JOB_OBJECT_LIMIT_JOB_MEMORY
    /// 的作业后，进程分配内存超过上限时会被系统直接拒绝（表现为分配失败），
    /// 这是操作系统层面的硬限制，任务管理器里也能看到内存涨不过这个数。
    ///
    /// 注意：作业句柄必须一直保持打开，关掉它限制就没了，
    /// 所以调用方要持有返回的 AppliedLimit 直到游戏退出。
    /// </summary>
    public static class JobMemoryLimiter
    {
        private const uint JOB_OBJECT_LIMIT_PROCESS_MEMORY = 0x00000100;
        private const uint JOB_OBJECT_LIMIT_JOB_MEMORY = 0x00000200;
        private const int JobObjectExtendedLimitInformation = 9;

        /// <summary>一次已经生效的内存上限。Dispose 会释放作业句柄（限制随之解除）。</summary>
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

        /// <summary>
        /// 给进程施加内存上限。成功返回句柄持有对象，失败返回 null（不抛异常）。
        /// </summary>
        public static AppliedLimit? Apply(Process process, int limitMb)
        {
            if (process == null || limitMb <= 0) return null;

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

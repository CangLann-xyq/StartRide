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

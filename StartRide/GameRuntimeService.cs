using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace StartRide.Core
{
    /// <summary>
    /// BeamNG.drive 进程的查询与收尾。
    ///
    /// 两个实际用途：
    ///   1. 首页/设置页显示"游戏是否在运行"（判断游玩时长也靠它）
    ///   2. 游戏崩溃或没退干净时，残留进程会让下次启动报"已经在运行"，
    ///      这里提供一键结束（只结束 BeamNG.drive.exe，不碰别的进程）
    /// </summary>
    public static class GameRuntimeService
    {
        /// <summary>BeamNG.drive.exe 的进程名（Process.ProcessName 不带扩展名）。</summary>
        public const string GameProcessName = "BeamNG.drive";

        public static IReadOnlyList<Process> GetRunningProcesses()
        {
            try
            {
                return Process.GetProcessesByName(GameProcessName);
            }
            catch
            {
                return Array.Empty<Process>();
            }
        }

        public static bool IsRunning()
        {
            var list = GetRunningProcesses();
            try
            {
                return list.Any(p => !p.HasExited);
            }
            catch
            {
                return list.Count > 0;
            }
            finally
            {
                foreach (var p in list) p.Dispose();
            }
        }

        /// <summary>返回第一个游戏进程的启动时间（本地时区）；拿不到返回 null。</summary>
        public static DateTimeOffset? TryGetStartTime()
        {
            foreach (var p in GetRunningProcesses())
            {
                try
                {
                    return new DateTimeOffset(p.StartTime);
                }
                catch
                {
                    // 权限/时序取不到就换下一个
                }
                finally
                {
                    p.Dispose();
                }
            }
            return null;
        }

        /// <summary>
        /// 结束全部 BeamNG.drive 进程（含子进程）。返回结束掉的进程数。
        /// 先请求关闭再强杀，给游戏一点时间自己收尾。
        /// </summary>
        public static int EndGame(int gracefulWaitMs = 6000)
        {
            int ended = 0;
            foreach (var p in GetRunningProcesses())
            {
                try
                {
                    p.CloseMainWindow();
                    if (p.WaitForExit(gracefulWaitMs))
                    {
                        ended++;
                        continue;
                    }
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(gracefulWaitMs);
                    ended++;
                }
                catch
                {
                    // 单个进程结束失败不影响其它
                }
                finally
                {
                    p.Dispose();
                }
            }
            return ended;
        }
    }
}

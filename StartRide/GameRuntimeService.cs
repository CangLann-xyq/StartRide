using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace StartRide.Core
{

    public static class GameRuntimeService
    {
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
                }
                finally
                {
                    p.Dispose();
                }
            }
            return null;
        }

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

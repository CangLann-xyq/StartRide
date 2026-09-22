using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace StartRide.Core
{
    /// <summary>一条自定义命令的执行结果。</summary>
    public sealed class CommandResult
    {
        public bool Started { get; init; }
        public int ExitCode { get; init; } = -1;
        public string Output { get; init; } = "";
        public string Error { get; init; } = "";
        public bool TimedOut { get; init; }

        public string Summary =>
            !Started ? "未能启动"
            : TimedOut ? "超时"
            : ExitCode == 0 ? "成功"
            : "退出码 " + ExitCode;
    }

    /// <summary>
    /// 执行用户在设置里填的「启动前命令 / 退出后游戏命令」。
    ///
    /// 这些设置以前在「内存与启动」页里能填、能存，但没有任何代码读它们，
    /// 填了等于没填。这里统一走 cmd.exe /c，支持用户写管道、&amp;&amp;、引号路径。
    /// </summary>
    public static class CommandRunner
    {
        /// <summary>默认超时：够长的启动脚本也能跑完，又不会把界面卡死。</summary>
        public const int DefaultTimeoutMs = 120_000;

        /// <summary>
        /// 同步执行并等它结束。
        /// </summary>
        public static CommandResult Run(string? commandLine, int timeoutMs = DefaultTimeoutMs)
        {
            var r = RunAsync(commandLine, wait: true, timeoutMs).ConfigureAwait(false);
            return r.GetAwaiter().GetResult();
        }

        /// <summary>
        /// 执行命令。wait=false 时只负责拉起来，不收集输出（用于"不等它结束"的启动前命令）。
        /// </summary>
        public static async Task<CommandResult> RunAsync(string? commandLine, bool wait, int timeoutMs = DefaultTimeoutMs)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return new CommandResult { Started = false, Error = "命令为空" };
            }

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = wait,
                RedirectStandardError = wait,
            };
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(commandLine.Trim());

            try
            {
                using var p = Process.Start(psi);
                if (p == null) return new CommandResult { Started = false, Error = "Process.Start 返回空" };

                if (!wait)
                {
                    return new CommandResult { Started = true, ExitCode = 0, Output = "已拉起（未等待）" };
                }

                using var cts = new CancellationTokenSource(timeoutMs);
                var outTask = p.StandardOutput.ReadToEndAsync();
                var errTask = p.StandardError.ReadToEndAsync();

                try
                {
                    await p.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
                    return new CommandResult
                    {
                        Started = true,
                        TimedOut = true,
                        ExitCode = -1,
                        Error = $"执行超过 {timeoutMs / 1000} 秒，已终止",
                    };
                }

                string stdout = await SafeAwait(outTask).ConfigureAwait(false);
                string stderr = await SafeAwait(errTask).ConfigureAwait(false);

                return new CommandResult
                {
                    Started = true,
                    ExitCode = p.ExitCode,
                    Output = Trim(stdout),
                    Error = Trim(stderr),
                };
            }
            catch (Exception ex)
            {
                return new CommandResult { Started = false, Error = ex.Message };
            }
        }

        private static async Task<string> SafeAwait(Task<string> t)
        {
            try { return await t.ConfigureAwait(false); }
            catch { return ""; }
        }

        private static string Trim(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Trim();
            // 界面/日志里只留最后一小段，避免刷屏
            return s.Length <= 2000 ? s : s[^2000..];
        }
    }
}

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace StartRide.Core
{
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

    public static class CommandRunner
    {
        public const int DefaultTimeoutMs = 120_000;

        public static CommandResult Run(string? commandLine, int timeoutMs = DefaultTimeoutMs)
        {
            var r = RunAsync(commandLine, wait: true, timeoutMs).ConfigureAwait(false);
            return r.GetAwaiter().GetResult();
        }

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
            return s.Length <= 2000 ? s : s[^2000..];
        }
    }
}

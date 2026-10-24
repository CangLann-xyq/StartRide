using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StartRide.Core
{
    /// <summary>执行一条工具调用的结果（给模型看 + 给 UI 显示）。</summary>
    public sealed class AiToolResult
    {
        public bool Ok { get; init; }
        public string Output { get; init; } = "";
        public bool Denied { get; init; }
    }

    /// <summary>三态授权。</summary>
    public enum AiPermission
    {
        /// <summary>禁止：直接拒绝，不问。</summary>
        Deny = 0,

        /// <summary>每次问我：执行前把「要干什么」摊给用户，点了才动。</summary>
        Ask = 1,

        /// <summary>直接允许：不再逐次确认。</summary>
        Allow = 2,
    }

    /// <summary>一次「要动你电脑了，允许吗」的询问（UI 拿它渲染确认条）。</summary>
    public sealed class AiConfirmRequest
    {
        public string Action { get; init; } = "";     // 读取文件 / 写入文件 / 执行命令
        public string Tool { get; init; } = "";       // 中文工具名
        public string Target { get; init; } = "";     // 具体路径或命令原文
        public string Extra { get; init; } = "";      // 附加说明（覆盖已有文件、命令风险等）
    }

    /// <summary>
    /// Agent 模式能碰电脑的唯一入口 —— 所有危险动作都从这里过一遍权限与路径检查。
    ///
    /// 权限模型（三态，存在 AppSettings.AiPolicy*）：
    ///   Deny   直接拒绝
    ///   Ask    回调 UI 问一次（用户点「允许」才动）
    ///   Allow  直接放行
    /// 三类能力：读（列目录/读文件）、写、执行命令。
    ///
    /// 路径模型：
    ///   所有路径必须落在 AiWorkspaceRoot 里（默认用户目录），
    ///   越界直接拒绝 —— 不然模型一句 "读 C:\Windows\..." 就能把系统翻一遍。
    ///
    /// 为什么既有 Ask 又保留 Allow：
    ///   Agent 修环境时会连着调十几次工具（列目录→读文件→改配置→再读一遍验证），
    ///   每步弹窗能把人烦死，所以「这一轮都别问」是必要的；
    ///   但默认应该是 Ask，让用户先看清它到底要改哪个文件。
    /// </summary>
    public sealed class AiToolHost
    {
        /// <summary>单条命令最长跑多久；超时直接杀进程树。</summary>
        public const int CommandTimeoutSeconds = 60;

        /// <summary>命令 / 文件输出回给模型的长度上限（防止把上下文撑爆）。</summary>
        private const int MaxOutputChars = 12000;

        private static readonly string[] BannedFragments =
        {
            "format ", "diskpart", "bcdedit", "shutdown", "cipher /w",
            "del /s", "del /f /s", "del /q /s", "rmdir /s", "rd /s",
            "reg delete", "takeown", "vssadmin", "wbadmin", "net user",
            "netsh advfirewall set", "sfc /scannow", "chkdsk",
        };

        // ------------------------------------------------------------------
        // 工具清单（OpenAI function calling 格式，直接塞进请求体）
        // ------------------------------------------------------------------
        public static List<Dictionary<string, object?>> BuildToolSchema()
        {
            return new List<Dictionary<string, object?>>
            {
                Fn("list_dir", "列出目录下的文件和子目录（只看一层）。修改文件前先用它确认路径。",
                    new() { ["path"] = Prop("string", "目录路径；相对路径按工作目录解析") },
                    new[] { "path" }),

                Fn("read_file", "读取文本文件内容。二进制文件会返回提示。",
                    new()
                    {
                        ["path"] = Prop("string", "文件路径"),
                        ["max_bytes"] = Prop("integer", "最多读多少字节，默认 200000"),
                    },
                    new[] { "path" }),

                Fn("write_file", "写入（或覆盖）文本文件。会先自动创建父目录。写完请复述改了哪个文件。",
                    new()
                    {
                        ["path"] = Prop("string", "文件路径"),
                        ["content"] = Prop("string", "完整的新内容（不要只写片段）"),
                    },
                    new[] { "path", "content" }),

                Fn("run_command", "在 Windows cmd 里执行一条命令，返回 stdout+stderr。适合查版本、跑构建、诊断环境。",
                    new()
                    {
                        ["command"] = Prop("string", "要执行的命令（不带 cmd /c 前缀）"),
                        ["timeout_seconds"] = Prop("integer", "超时秒数，默认 60，最多 60。"),
                    },
                    new[] { "command" }),
            };
        }

        private static Dictionary<string, object?> Fn(string name, string desc,
            Dictionary<string, object?> props, string[] required)
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "function",
                ["function"] = new Dictionary<string, object?>
                {
                    ["name"] = name,
                    ["description"] = desc,
                    ["parameters"] = new Dictionary<string, object?>
                    {
                        ["type"] = "object",
                        ["properties"] = props,
                        ["required"] = required,
                    },
                },
            };
        }

        private static Dictionary<string, object?> Prop(string type, string desc)
            => new() { ["type"] = type, ["description"] = desc };

        // ------------------------------------------------------------------
        // 执行入口
        // ------------------------------------------------------------------
        /// <param name="confirm">
        /// 三态里「Ask」时用它问用户；返回 true = 允许这一次。
        /// 传 null 时 Ask 一律当拒绝（安全默认：没人能确认就不动）。
        /// </param>
        public async Task<AiToolResult> ExecuteAsync(
            string name,
            string argumentsJson,
            CancellationToken ct = default,
            Func<AiConfirmRequest, Task<bool>>? confirm = null)
        {
            try
            {
                using var doc = string.IsNullOrWhiteSpace(argumentsJson)
                    ? JsonDocument.Parse("{}")
                    : JsonDocument.Parse(argumentsJson);
                var args = doc.RootElement;

                switch (name)
                {
                    case "list_dir":
                    {
                        var path = Str(args, "path");
                        var g = await GuardAsync(name, path, "", ct, confirm);
                        return g ?? ListDir(path);
                    }
                    case "read_file":
                    {
                        var path = Str(args, "path");
                        var g = await GuardAsync(name, path, "", ct, confirm);
                        return g ?? ReadFile(path, Int(args, "max_bytes", 200_000));
                    }
                    case "write_file":
                    {
                        var path = Str(args, "path");
                        var content = Str(args, "content");
                        var full = ResolvePath(path);
                        var extra = File.Exists(full)
                            ? $"将覆盖已有文件（当前 {FormatSize(new FileInfo(full).Length)}）"
                            : "将新建文件";
                        var g = await GuardAsync(name, path, extra, ct, confirm);
                        return g ?? WriteFile(path, content);
                    }
                    case "run_command":
                    {
                        var cmd = Str(args, "command");
                        var g = await GuardAsync(name, "", cmd, ct, confirm);
                        return g ?? await RunCommandAsync(cmd, Int(args, "timeout_seconds", CommandTimeoutSeconds), ct);
                    }
                    default:
                        return new AiToolResult { Ok = false, Output = "未知工具：" + name };
                }
            }
            catch (OperationCanceledException)
            {
                throw;   // 取消要往上抛，不能让 VM 当成普通失败
            }
            catch (JsonException)
            {
                return new AiToolResult { Ok = false, Output = "参数不是合法 JSON：" + Preview(argumentsJson) };
            }
            catch (Exception ex)
            {
                return new AiToolResult { Ok = false, Output = ex.GetType().Name + "：" + ex.Message };
            }
        }

        /// <summary>工具名 → 它需要的那个能力策略。</summary>
        public static AiPermission PolicyOf(string tool)
        {
            var s = AppSettings.Current;
            return tool switch
            {
                "list_dir" or "read_file" => ReadPolicy(s),
                "write_file" => WritePolicy(s),
                "run_command" => ExecPolicy(s),
                _ => AiPermission.Deny,
            };
        }

        // 老配置（三个 int? 都是 null）时从 AiAllow* 推导：原来开着 = 直接允许，
        // 而不是「每次问我」—— 升级不该悄悄改变用户已经习惯的行为。
        public static AiPermission ReadPolicy(AppSettings s)
            => Clamp(s.AiPolicyRead ?? (s.AiAllowRead ? 2 : 0));

        public static AiPermission WritePolicy(AppSettings s)
            => Clamp(s.AiPolicyWrite ?? (s.AiAllowWrite ? 2 : 0));

        public static AiPermission ExecPolicy(AppSettings s)
            => Clamp(s.AiPolicyExecute ?? (s.AiAllowExecute ? 2 : 0));

        private static AiPermission Clamp(int v)
            => v <= 0 ? AiPermission.Deny : (v == 1 ? AiPermission.Ask : AiPermission.Allow);

        // ------------------------------------------------------------------
        // 具体工具
        // ------------------------------------------------------------------
        private AiToolResult ListDir(string path)
        {
            var full = ResolvePath(path);
            if (!Directory.Exists(full))
                return new AiToolResult { Ok = false, Output = "目录不存在：" + full };

            // 条目必须在**工具层**就收窄：一个普通用户目录有几百个文件，
            // 原样返回会把模型上下文和 HUD 一起冲爆（实测截图里整屏都是文件名）。
            var dirs = SafeEnumerate(() => Directory.GetDirectories(full), 40).ToList();
            var files = SafeEnumerate(() => Directory.GetFiles(full), 81).ToList();
            var fileTruncated = files.Count > 80;
            if (fileTruncated) files.RemoveAt(files.Count - 1);

            var sb = new StringBuilder();
            sb.AppendLine("目录：" + full);
            foreach (var d in dirs)
                sb.AppendLine("  [目录] " + Path.GetFileName(d));
            foreach (var f in files)
            {
                var info = new FileInfo(f);
                sb.AppendLine($"  {info.Name}  ({FormatSize(info.Length)})");
            }
            if (dirs.Count >= 40) sb.AppendLine("  …（子目录过多，只列了前 40 个）");
            if (fileTruncated) sb.AppendLine("  …（文件过多，只列了前 80 个）");
            return new AiToolResult { Ok = true, Output = Cap(sb.ToString()) };
        }

        private AiToolResult ReadFile(string path, int maxBytes)
        {
            var full = ResolvePath(path);
            if (!File.Exists(full))
                return new AiToolResult { Ok = false, Output = "文件不存在：" + full };

            var info = new FileInfo(full);
            using var fs = File.OpenRead(full);
            var take = (int)Math.Min(Math.Max(1024, maxBytes), Math.Min(info.Length, MaxOutputChars * 4));
            var buf = new byte[take];
            var read = fs.Read(buf, 0, take);

            // 粗略判断二进制：出现 NUL 就当二进制，别把乱码塞进上下文
            for (var i = 0; i < read; i++)
            {
                if (buf[i] == 0)
                    return new AiToolResult
                    {
                        Ok = false,
                        Output = $"这是二进制文件（{FormatSize(info.Length)}），没有可读文本内容。",
                    };
            }

            var text = Encoding.UTF8.GetString(buf, 0, read);
            var tail = info.Length > read ? $"\n…（只读了前 {read} 字节，全文 {FormatSize(info.Length)}）" : "";
            return new AiToolResult { Ok = true, Output = Cap(text) + tail };
        }

        private AiToolResult WriteFile(string path, string content)
        {
            var full = ResolvePath(path);
            var dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var existed = File.Exists(full);
            var oldSize = existed ? new FileInfo(full).Length : 0;
            File.WriteAllText(full, content, new UTF8Encoding(false));
            var newSize = new FileInfo(full).Length;

            return new AiToolResult
            {
                Ok = true,
                Output = (existed ? "已覆盖 " : "已新建 ") + full
                         + $"\n大小：{FormatSize(oldSize)} → {FormatSize(newSize)}（{content.Length} 字符）",
            };
        }

        private async Task<AiToolResult> RunCommandAsync(string command, int timeoutSeconds, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(command))
                return new AiToolResult { Ok = false, Output = "命令为空" };

            foreach (var bad in BannedFragments)
            {
                if (command.IndexOf(bad, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return new AiToolResult
                    {
                        Ok = false,
                        Output = $"命令里出现被禁止的片段「{bad.Trim()}」——这类操作会破坏系统，已拒绝。"
                                 + "如果确实需要，请让用户自己手动执行。",
                    };
                }
            }

            var seconds = Math.Clamp(timeoutSeconds <= 0 ? CommandTimeoutSeconds : timeoutSeconds, 5, CommandTimeoutSeconds);
            var root = WorkspaceRoot();

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c chcp 65001>nul & " + command,
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using var proc = new Process { StartInfo = psi };
            var sw = Stopwatch.StartNew();
            try
            {
                proc.Start();
            }
            catch (Exception ex)
            {
                return new AiToolResult { Ok = false, Output = "启动失败：" + ex.Message };
            }

            var stdout = proc.StandardOutput.ReadToEndAsync();
            var stderr = proc.StandardError.ReadToEndAsync();

            // 取消 = 立刻杀进程树。只 cancel 读流没用：
            // WaitForExit 是阻塞调用，不响应 token，会一直等到命令自己结束。
            // 这就是「点了停止但任务还在跑」的直接原因。
            using var reg = ct.Register(() =>
            {
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
            });

            var finished = await Task.Run(() => proc.WaitForExit(seconds * 1000));

            if (ct.IsCancellationRequested)
            {
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
                throw new OperationCanceledException(ct);
            }
            if (!finished)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                return new AiToolResult
                {
                    Ok = false,
                    Output = $"命令超时（>{seconds}s），已结束进程。\n命令：{command}",
                };
            }

            var sb = new StringBuilder();
            sb.AppendLine("$ " + command);
            sb.AppendLine("(工作目录 " + root + ")");
            var o = await stdout;
            var e = await stderr;
            if (!string.IsNullOrWhiteSpace(o)) sb.AppendLine(o.TrimEnd());
            if (!string.IsNullOrWhiteSpace(e)) sb.AppendLine("[stderr] " + e.TrimEnd());
            sb.AppendLine("退出码：" + proc.ExitCode + "  用时：" + sw.ElapsedMilliseconds + " ms");

            return new AiToolResult { Ok = proc.ExitCode == 0, Output = Cap(sb.ToString()) };
        }

        // ------------------------------------------------------------------
        // 权限 / 路径围栏
        // ------------------------------------------------------------------
        /// <summary>
        /// 三态门。返回 null = 放行（继续干活）；返回值 = 直接把它当结果返回。
        ///
        /// Nullable 返回值在这里是必要的：拒绝要返回「一句给模型看的话」，
        /// 但单纯 bool 表达不了这句话，调用处还得再拼一次，容易两边措辞不一致。
        /// </summary>
        private static async Task<AiToolResult?> GuardAsync(
            string tool, string target, string extra, CancellationToken ct,
            Func<AiConfirmRequest, Task<bool>>? confirm)
        {
            var policy = PolicyOf(tool);
            var action = ActionOf(tool);

            if (policy == AiPermission.Allow) return null;

            if (policy == AiPermission.Deny)
            {
                return new AiToolResult
                {
                    Ok = false,
                    Denied = true,
                    Output = $"【权限不足】当前策略禁止{action}。"
                             + "请让用户在 AI 助手右侧的「权限」里把这一项改成「询问」或「允许」后再试，不要尝试绕过。",
                };
            }

            // Ask：没有确认通道就当拒绝 —— 宁可不动，也不能「问不到就自己决定」
            if (confirm == null)
            {
                return new AiToolResult
                {
                    Ok = false,
                    Denied = true,
                    Output = $"【未确认】{action}需要用户确认，但当前没有可用的确认界面，已跳过。",
                };
            }

            ct.ThrowIfCancellationRequested();
            var allowed = await confirm(new AiConfirmRequest
            {
                Action = action,
                Tool = DisplayName(tool),
                Target = target,
                Extra = extra,
            });

            if (allowed) return null;

            return new AiToolResult
            {
                Ok = false,
                Denied = true,
                Output = $"【用户拒绝】用户没有允许这次{action}。"
                         + "不要再重复调用同一个工具，直接把情况告诉用户，或者换个不需要许可的做法。",
            };
        }

        private static string ActionOf(string tool) => tool switch
        {
            "list_dir" or "read_file" => "读取文件",
            "write_file" => "写入文件",
            "run_command" => "执行命令",
            _ => "该操作",
        };

        public static string WorkspaceRoot()
        {
            var root = AppSettings.Current.AiWorkspaceRoot;
            if (string.IsNullOrWhiteSpace(root))
                root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            try { root = Path.GetFullPath(root); } catch { root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); }
            return root;
        }

        private static string ResolvePath(string path)
        {
            var root = WorkspaceRoot();
            if (string.IsNullOrWhiteSpace(path)) return root;
            var full = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));
            var rootWithSep = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.Equals(root, StringComparison.OrdinalIgnoreCase)
                && !full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"路径超出工作目录：{full}\n允许的根目录是 {root}（可在 AI 助手右侧修改）");
            }
            return full;
        }

        // ------------------------------------------------------------------
        // 小工具
        // ------------------------------------------------------------------
        private static IEnumerable<string> SafeEnumerate(Func<string[]> get, int limit)
        {
            string[] arr;
            try { arr = get(); } catch { yield break; }
            Array.Sort(arr, StringComparer.OrdinalIgnoreCase);
            var n = 0;
            foreach (var s in arr)
            {
                if (n++ >= limit) yield break;
                yield return s;
            }
        }

        private static string Str(JsonElement e, string name)
            => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";

        private static int Int(JsonElement e, string name, int dflt)
        {
            if (!e.TryGetProperty(name, out var v)) return dflt;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
            if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var m)) return m;
            return dflt;
        }

        private static string Cap(string s)
            => s.Length <= MaxOutputChars ? s : s.Substring(0, MaxOutputChars) + "\n…（输出过长已截断）";

        private static string Preview(string s)
            => string.IsNullOrEmpty(s) ? "(空)" : (s.Length <= 120 ? s : s.Substring(0, 120) + "…");

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            return (bytes / 1024.0 / 1024.0).ToString("0.0") + " MB";
        }

        /// <summary>给 UI 用的中文名（执行过程面板显示这个，而不是 list_dir 这种）。</summary>
        public static string DisplayName(string tool) => tool switch
        {
            "list_dir" => "列目录",
            "read_file" => "读文件",
            "write_file" => "写文件",
            "run_command" => "执行命令",
            _ => tool,
        };
    }
}

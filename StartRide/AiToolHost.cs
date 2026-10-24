using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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

    /// <summary>
    /// Agent 模式能碰电脑的唯一入口 —— 所有危险动作都从这里过一遍权限与路径检查。
    ///
    /// 权限模型（对应启动器里的三个开关，都存在 AppSettings）：
    ///   AiAllowRead     读文件 / 列目录
    ///   AiAllowWrite    写文件（新建 / 覆盖）
    ///   AiAllowExecute  执行命令
    /// 路径模型：
    ///   所有路径必须落在 AiWorkspaceRoot 里（默认用户目录），
    ///   越界直接拒绝 —— 不然模型一句 "读 C:\Windows\..." 就能把系统翻一遍。
    ///
    /// 为什么不给「每次弹窗确认」：
    ///   Agent 一轮任务会连续调十几次工具，每步弹窗等于没法用。
    ///   所以采用「按能力授权 + 路径围栏 + 命令黑名单」，
    ///   用户随时可以关掉开关，关掉后模型会收到明确的拒绝原因。
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
        public async Task<AiToolResult> ExecuteAsync(string name, string argumentsJson)
        {
            try
            {
                using var doc = string.IsNullOrWhiteSpace(argumentsJson)
                    ? JsonDocument.Parse("{}")
                    : JsonDocument.Parse(argumentsJson);
                var args = doc.RootElement;

                switch (name)
                {
                    case "list_dir": return ListDir(Str(args, "path"));
                    case "read_file": return ReadFile(Str(args, "path"), Int(args, "max_bytes", 200_000));
                    case "write_file": return WriteFile(Str(args, "path"), Str(args, "content"));
                    case "run_command": return await RunCommandAsync(Str(args, "command"), Int(args, "timeout_seconds", CommandTimeoutSeconds));
                    default:
                        return new AiToolResult { Ok = false, Output = "未知工具：" + name };
                }
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

        // ------------------------------------------------------------------
        // 具体工具
        // ------------------------------------------------------------------
        private AiToolResult ListDir(string path)
        {
            if (!Guard("读取文件", AppSettings.Current.AiAllowRead, out var denied)) return denied;
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
            if (!Guard("读取文件", AppSettings.Current.AiAllowRead, out var denied)) return denied;
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
            if (!Guard("写入文件", AppSettings.Current.AiAllowWrite, out var denied)) return denied;
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

        private async Task<AiToolResult> RunCommandAsync(string command, int timeoutSeconds)
        {
            if (!Guard("执行命令", AppSettings.Current.AiAllowExecute, out var denied)) return denied;
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

            var finished = await Task.Run(() => proc.WaitForExit(seconds * 1000));
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
        private static bool Guard(string action, bool allowed, out AiToolResult denied)
        {
            if (allowed)
            {
                denied = new AiToolResult();
                return true;
            }
            denied = new AiToolResult
            {
                Ok = false,
                Denied = true,
                Output = $"【权限不足】当前不允许{action}。请让用户在 AI 助手右侧的「权限」里打开对应开关后再试，"
                         + "不要尝试绕过。",
            };
            return false;
        }

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

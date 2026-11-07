using System;
using System.IO;
using System.Text.Json;

namespace StartRide.Services
{
    /// <summary>
    /// 记录「本机已经成功装上过的内部构建号」。
    ///
    /// 版本号固定 1.0.0（按项目约定不升版本），所以正式版之后的构建更新靠清单里的
    /// versionCode 递增来推送。但如果只比较 versionCode：装完之后 BuildInfo.Version
    /// 还是 1.0.0（算出 10000），远程仍是 10001，每次启动都会再弹一次「发现新版本」——
    /// 装完还是同一版，死循环。这份状态就是给 IsNewer 一个「已经装过了」的下限。
    ///
    /// 由更新安装脚本在复制文件成功后写入；安装失败不写，用户可以重试。
    /// </summary>
    public static class StartRideUpdateState
    {
        private static readonly object Gate = new();

        /// <summary>与安装脚本里写的是同一个文件，两边字段名必须一致。</summary>
        public const string StateFileName = "update-state.json";

        public const string StatePropertyName = "appliedVersionCode";

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StartRide", StateFileName);

        public static int ReadAppliedCode()
        {
            lock (Gate)
            {
                return ReadAppliedCodeUnlocked();
            }
        }

        /// <summary>供安装脚本之外的地方（例如人工修数据）调用；只在成功时抬高，不回退。</summary>
        public static void WriteAppliedCode(int code)
        {
            if (code <= 0) return;
            lock (Gate)
            {
                try
                {
                    if (code <= ReadAppliedCodeUnlocked()) return;
                    string? directory = Path.GetDirectoryName(FilePath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    string json = JsonSerializer.Serialize(new AppliedPayload { appliedVersionCode = code });
                    string temp = FilePath + ".tmp";
                    File.WriteAllText(temp, json);
                    if (File.Exists(FilePath)) File.Delete(FilePath);
                    File.Move(temp, FilePath);
                }
                catch
                {
                    // 状态写失败不该让启动器起不来；最多是下次多弹一次更新。
                }
            }
        }

        private static int ReadAppliedCodeUnlocked()
        {
            try
            {
                if (!File.Exists(FilePath)) return 0;
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(FilePath));
                JsonElement root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty(StatePropertyName, out JsonElement value) &&
                    value.ValueKind == JsonValueKind.Number &&
                    value.TryGetInt32(out int code) && code > 0)
                {
                    return code;
                }
            }
            catch
            {
                // 文件损坏按「没装过」处理。
            }
            return 0;
        }

        private sealed class AppliedPayload
        {
            public int appliedVersionCode { get; set; }
        }
    }
}

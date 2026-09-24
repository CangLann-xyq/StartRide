using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace StartRide.Core
{
    /// <summary>一个"游戏"条目（沿用原版的 instance 模型）。</summary>
    public sealed class GameInstance
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Name { get; set; } = "";
        /// <summary>游戏根目录（含 BeamNG.drive.exe 的那一层）。</summary>
        public string Directory { get; set; } = "";
        /// <summary>图标键，对应 GameSettings_Icon* 文案（Anvil/Beacon/...）。</summary>
        public string IconKey { get; set; } = "GrassBlock";
        /// <summary>来源渠道：Release / Snapshot / AprilFools / Ancient / LocalImport。</summary>
        public string Channel { get; set; } = "Release";
        public string Version { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string ExecutablePath => Path.Combine(Directory, "BeamNG.drive.exe");
        public bool IsValid => Directory.Length > 0 && File.Exists(ExecutablePath);

        /// <summary>
        /// 版本号：优先用登记时记下的值，其次读 version.txt；
        /// 都拿不到时看目录名像不像版本号（0.36.x 这种），不像就算未知。
        /// </summary>
        public string ResolvedVersion
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Version)) return Version;
                try
                {
                    string vt = Path.Combine(Directory, "version.txt");
                    if (File.Exists(vt))
                    {
                        string raw = File.ReadAllText(vt).Trim();
                        if (raw.Length > 0) return raw;
                    }
                }
                catch { }

                string leaf = Path.GetFileName(Directory.TrimEnd('\\', '/'));
                return LooksLikeVersion(leaf) ? leaf : "未知版本";
            }
        }

        internal static bool LooksLikeVersion(string s)
        {
            if (s.Length == 0) return false;
            if (!char.IsDigit(s[0])) return false;
            foreach (var c in s)
                if (!char.IsDigit(c) && c != '.' && c != '-' && c != '_' && c != 'x' && c != 'X')
                    return false;
            return true;
        }
    }

    /// <summary>
    /// 已安装游戏清单。持久化到 %AppData%\StartRide\instances.json。
    ///
    /// 首次运行会自动把「全局设置里探测到的游戏目录」导入成第一条记录，
    /// 这样老用户升级上来不会看到空列表。
    /// </summary>
    public sealed class InstanceStore
    {
        public static string StorePath => Path.Combine(AppSettings.ConfigDirectory, "instances.json");

        private readonly AppSettings _settings;

        public List<GameInstance> Items { get; private set; } = new();

        public event Action? Changed;
        public event Action<string>? Log;

        public InstanceStore(AppSettings settings)
        {
            _settings = settings;
            Load();
        }

        public GameInstance? Active =>
            Items.FirstOrDefault(i => i.Id == _settings.ActiveInstanceId) ?? Items.FirstOrDefault();

        public void Load()
        {
            try
            {
                if (File.Exists(StorePath))
                {
                    var list = JsonSerializer.Deserialize<List<GameInstance>>(File.ReadAllText(StorePath));
                    if (list != null) Items = list;
                }
            }
            catch
            {
                Items = new List<GameInstance>();
            }

            // 首次：把全局设置里的目录接管进来
            if (Items.Count == 0 && !string.IsNullOrWhiteSpace(_settings.GameDirectory) &&
                File.Exists(Path.Combine(_settings.GameDirectory, "BeamNG.drive.exe")))
            {
                Items.Add(new GameInstance
                {
                    Name = "BeamNG.drive",
                    Directory = _settings.GameDirectory,
                    Channel = "Release",
                });
                Save();
            }

            if (Items.Count > 0 && Items.All(i => i.Id != _settings.ActiveInstanceId))
            {
                _settings.ActiveInstanceId = Items[0].Id;
                _settings.Save();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppSettings.ConfigDirectory);
                File.WriteAllText(StorePath, JsonSerializer.Serialize(Items,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Log?.Invoke("保存游戏列表失败：" + ex.Message);
            }
            Changed?.Invoke();
        }

        /// <summary>注册一个目录为游戏条目；已存在同目录则直接返回旧条目。</summary>
        public GameInstance Add(string directory, string? name = null, string channel = "LocalImport")
        {
            string full = Path.GetFullPath(directory);
            var existing = Items.FirstOrDefault(i =>
                string.Equals(i.Directory, full, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;

            var inst = new GameInstance
            {
                Name = string.IsNullOrWhiteSpace(name) ? DefaultNameFor(full) : name!,
                Directory = full,
                Channel = channel,
            };

            try
            {
                string vt = Path.Combine(full, "version.txt");
                if (File.Exists(vt)) inst.Version = File.ReadAllText(vt).Trim();
            }
            catch { }

            Items.Add(inst);
            if (string.IsNullOrWhiteSpace(_settings.ActiveInstanceId)) _settings.ActiveInstanceId = inst.Id;
            _settings.Save();
            Save();
            Log?.Invoke($"已添加游戏条目：{inst.Name}");
            return inst;
        }

        private static string DefaultNameFor(string directory)
        {
            string leaf = Path.GetFileName(directory.TrimEnd('\\', '/'));
            return string.IsNullOrWhiteSpace(leaf) ? "BeamNG.drive" : leaf;
        }

        public void Remove(string id)
        {
            var inst = Items.FirstOrDefault(i => i.Id == id);
            if (inst == null) return;
            Items.Remove(inst);
            if (_settings.ActiveInstanceId == id)
            {
                _settings.ActiveInstanceId = Items.Count > 0 ? Items[0].Id : "";
                _settings.Save();
            }
            Save();
            Log?.Invoke($"已移除游戏条目：{inst.Name}");
        }

        public void SetActive(string id)
        {
            if (Items.All(i => i.Id != id)) return;
            _settings.ActiveInstanceId = id;
            var inst = Items.First(i => i.Id == id);
            // 全局目录跟着当前实例走，启动逻辑与模组安装都读这个值
            _settings.GameDirectory = inst.Directory;
            _settings.Save();
            Changed?.Invoke();
        }

        /// <summary>扫描常见安装位置，返回找到的、尚未登记的游戏目录。</summary>
        public List<string> ScanForInstallations()
        {
            var found = new List<string>();
            var candidates = new List<string>();

            foreach (var drive in new[] { "C:", "D:", "E:", "F:", "G:" })
            {
                candidates.Add($@"{drive}\BeamNG.drive");
                candidates.Add($@"{drive}\Games\BeamNG.drive");
                candidates.Add($@"{drive}\SteamLibrary\steamapps\common\BeamNG.drive");
                candidates.Add($@"{drive}\Program Files (x86)\Steam\steamapps\common\BeamNG.drive");
                candidates.Add($@"{drive}\Program Files\Steam\steamapps\common\BeamNG.drive");
                candidates.Add($@"{drive}\Program Files (x86)\SteamLibrary\steamapps\common\BeamNG.drive");
            }

            foreach (var c in candidates)
            {
                try
                {
                    if (!Directory.Exists(c)) continue;
                    if (!File.Exists(Path.Combine(c, "BeamNG.drive.exe"))) continue;
                    string full = Path.GetFullPath(c);
                    if (Items.Any(i => string.Equals(i.Directory, full, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    if (found.Contains(full, StringComparer.OrdinalIgnoreCase)) continue;
                    found.Add(full);
                }
                catch { }
            }
            return found;
        }

        /// <summary>
        /// 从压缩包导入：解压到 %AppData%\StartRide\instances\&lt;名字&gt;，
        /// 解压后若在子目录里发现 BeamNG.drive.exe 就自动下钻一层。
        /// </summary>
        public string? ImportArchive(string archivePath, string? displayName, out string error)
        {
            error = "";
            try
            {
                string name = string.IsNullOrWhiteSpace(displayName)
                    ? Path.GetFileNameWithoutExtension(archivePath)
                    : displayName!;
                name = Sanitize(name);

                string target = Path.Combine(AppSettings.ConfigDirectory, "instances", name);
                Directory.CreateDirectory(target);

                using (var zip = ZipFile.OpenRead(archivePath))
                {
                    foreach (var e in zip.Entries)
                    {
                        string rel = e.FullName.Replace('/', Path.DirectorySeparatorChar);
                        string dest = Path.GetFullPath(Path.Combine(target, rel));
                        if (!dest.StartsWith(Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                            continue;   // 防御 zip 穿越
                        if (e.Name.Length == 0)
                        {
                            Directory.CreateDirectory(dest);
                            continue;
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        e.ExtractToFile(dest, overwrite: true);
                    }
                }

                string real = FindGameRoot(target) ?? target;
                Add(real, name, "LocalImport");
                Log?.Invoke($"已从压缩包导入：{name}");
                return null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return ex.Message;
            }
        }

        /// <summary>在目录树里找含 BeamNG.drive.exe 的那一层（最多下钻两层）。</summary>
        public static string? FindGameRoot(string root)
        {
            try
            {
                if (File.Exists(Path.Combine(root, "BeamNG.drive.exe"))) return root;
                foreach (var sub in Directory.GetDirectories(root))
                {
                    if (File.Exists(Path.Combine(sub, "BeamNG.drive.exe"))) return sub;
                    foreach (var sub2 in Directory.GetDirectories(sub))
                        if (File.Exists(Path.Combine(sub2, "BeamNG.drive.exe"))) return sub2;
                }
            }
            catch { }
            return null;
        }

        private static string Sanitize(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Length == 0 ? "instance" : name;
        }
    }
}

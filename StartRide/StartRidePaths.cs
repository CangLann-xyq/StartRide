using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StartRide.Core
{

    public static class StartRidePaths
    {
        public static string Root =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StartRide");

        public static string SettingsFile => Path.Combine(Root, "settings.json");

        public static string Accounts => Path.Combine(Root, "accounts");

        public static string GameData => Path.Combine(Root, "game");

        public static string LauncherState => Path.Combine(Root, "launcher");

        public static string Log => Path.Combine(Root, "Log");
        public static string Avatars => Path.Combine(Root, "avatars");
        public static string Backups => Path.Combine(Root, "backups");
        public static string Diagnostics => Path.Combine(Root, "diagnostics");

        public static string LegacyAppDirectory => Path.Combine(Root, "app");

        public static readonly string[] LegacyDirectoryNames =
        {
            ".minecraft", "cache", "images", "tools", "BHL"
        };

        public static IEnumerable<string> LegacyDirectories()
        {
            yield return Path.Combine(Root, ".minecraft");
            yield return Path.Combine(LegacyAppDirectory, ".minecraft");
            yield return Path.Combine(AppContext.BaseDirectory, ".minecraft");
            yield return Path.Combine(AppContext.BaseDirectory, "BHL");
        }

        public static void MigrateLegacyLayout()
        {
            try { Directory.CreateDirectory(Root); } catch { }
            try { MoveIfMissing(Path.Combine(LegacyAppDirectory, "settings.json"), SettingsFile); } catch { }
            try { MoveDirectoryIfMissing(Path.Combine(LegacyAppDirectory, "accounts"), Accounts); } catch { }
        }

        public static void EnsureLayout()
        {
            foreach (string dir in new[] { Root, Accounts, GameData, Log, Avatars, Backups, Diagnostics, LauncherState })
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }
        }

        private static void MoveIfMissing(string source, string target)
        {
            if (!File.Exists(source) || File.Exists(target)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(source, target);
        }

        private static void MoveDirectoryIfMissing(string source, string target)
        {
            if (!Directory.Exists(source)) return;
            if (Directory.Exists(target))
            {
                foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(source, file);
                    string dest = Path.Combine(target, rel);
                    if (File.Exists(dest)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Move(file, dest);
                }
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            Directory.Move(source, target);
        }

        public static List<string> FindLegacyArtifacts()
        {
            var found = new List<string>();
            foreach (string dir in LegacyDirectories().Concat(new[]
                     {
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BHL"),
                         LegacyAppDirectory
                     }))
            {
                try { if (Directory.Exists(dir)) found.Add(dir); } catch { }
            }
            return found;
        }
    }
}

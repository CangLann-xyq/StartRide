using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace StartRide.Setup;

internal static class Payload
{
    private const string AppResource = "StartRide.Setup.Payload.app.zip";
    private const string UninstallerResource = "StartRide.Setup.Payload.uninstall.zip";

    private static Assembly Self => typeof(Payload).Assembly;

    public static bool HasPayload => Self.GetManifestResourceInfo(AppResource) != null;

    public static Stream OpenAppPackage()
    {
        Stream? s = Self.GetManifestResourceStream(AppResource);
        if (s == null)
        {
            throw new InvalidOperationException(
                "安装包内没有找到应用数据（payload）。请使用完整的安装包重新安装。");
        }
        return s;
    }

    public static string? TryExtractUninstaller(string installDir)
    {
        Stream? s = Self.GetManifestResourceStream(UninstallerResource);
        if (s == null) return null;

        string dir = installDir;
        Directory.CreateDirectory(dir);

        using (s)
        using (var zip = new ZipArchive(s, ZipArchiveMode.Read))
        {
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                string name = entry.FullName.Replace('\\', '/').TrimStart('/');
                if (name.Length == 0 || name.EndsWith("/", StringComparison.Ordinal)) continue;

                string dst = Path.Combine(dir, name.Replace('/', Path.DirectorySeparatorChar));
                string? parent = Path.GetDirectoryName(dst);
                if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                entry.ExtractToFile(dst, overwrite: true);
            }
        }

        string exe = ProductInfo.UninstallerPathIn(installDir);
        return File.Exists(exe) ? exe : null;
    }

    public static string DescribeSelf()
    {
        Assembly self = Self;
        Version? v = self.GetName().Version;
        return $"{ProductInfo.ProductName} 安装程序 {v?.ToString(3) ?? "?"}"
             + (HasPayload ? "" : "（不含应用数据）");
    }
}

using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace StartRide.Setup;

/// <summary>
/// payload 读取器。安装包把「整包 zip」和「卸载器 zip」当成嵌入资源带在身上，
/// 所以用户拿到的是一个文件，不需要旁边的 data 目录。
/// </summary>
internal static class Payload
{
    private const string AppResource = "StartRide.Setup.Payload.app.zip";
    private const string UninstallerResource = "StartRide.Setup.Payload.uninstall.zip";

    private static Assembly Self => typeof(Payload).Assembly;

    /// <summary>当前产物里有没有带 payload（卸载器产物没有）。</summary>
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

    /// <summary>
    /// 把内置的卸载器解到 <c>&lt;安装目录&gt;\Uninstall\</c>，返回 exe 路径。
    ///
    /// 为什么是 zip 而不是单个 exe：framework-dependent 的 WPF 程序少了
    /// <c>.runtimeconfig.json</c> 就起不来（报"找不到 runtimeconfig"）。
    /// 把 exe + runtimeconfig + deps 一起打进去才不会出现"卸载器点不开"。
    /// </summary>
    public static string? TryExtractUninstaller(string installDir)
    {
        Stream? s = Self.GetManifestResourceStream(UninstallerResource);
        if (s == null) return null;

        string dir = ProductInfo.UninstallerDirIn(installDir);
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

    /// <summary>自检：安装包本身是否完整可用。</summary>
    public static string DescribeSelf()
    {
        Assembly self = Self;
        Version? v = self.GetName().Version;
        return $"{ProductInfo.ProductName} 安装程序 {v?.ToString(3) ?? "?"}"
             + (HasPayload ? "" : "（不含应用数据）");
    }
}

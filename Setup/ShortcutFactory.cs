using System;
using System.IO;
using System.Runtime.InteropServices;

namespace StartRide.Setup;

/// <summary>
/// 快捷方式创建。
///
/// 走 <c>WScript.Shell</c> 的晚绑定 COM：这是 Windows 上最稳的一条路，
/// 不需要引用 COM 互操作程序集，也不需要管理员权限。
/// 自己手写 .lnk 二进制（IDList / LinkInfo / ExtraData 三段）能做，但一旦写错
/// 快捷方式就是"点不开"而不是"报错"，排查成本远高于省下的那点依赖 —— 不划算。
/// </summary>
internal static class ShortcutFactory
{
    private const int CLSID_Size = 0;

    /// <summary>创建一个 .lnk。失败返回 false，不抛 —— 快捷方式失败不该让整个安装失败。</summary>
    public static bool TryCreate(string linkPath, string targetPath, string workingDir,
                                 string description, string iconPath)
    {
        object? shell = null;
        try
        {
            // 目标目录不存在的话，WScript.Shell 的 Save() 会直接抛，快捷方式就没了。
            // 开始菜单那一层是我们自己拼的，第一次装的时候目录本来就还不存在。
            string? parent = Path.GetDirectoryName(linkPath);
            if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
            {
                Directory.CreateDirectory(parent);
            }

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return false;

            shell = Activator.CreateInstance(shellType);
            if (shell == null) return false;

            dynamic wsh = shell;
            dynamic link = wsh.CreateShortcut(linkPath);
            link.TargetPath = targetPath;
            link.WorkingDirectory = string.IsNullOrEmpty(workingDir)
                ? Path.GetDirectoryName(targetPath) ?? ""
                : workingDir;
            link.Description = description;
            if (!string.IsNullOrEmpty(iconPath))
            {
                link.IconLocation = iconPath + ",0";
            }
            link.Save();
            // 显式释放 COM 对象，否则安装程序退出前这几个 RCW 一直挂着。
            Marshal.FinalReleaseComObject(link);
            return File.Exists(linkPath);
        }
        catch
        {
            return false;
        }
        finally
        {
            if (shell != null)
            {
                try { Marshal.FinalReleaseComObject(shell); } catch { }
            }
        }
    }

    public static void TryDelete(string linkPath)
    {
        try
        {
            if (File.Exists(linkPath)) File.Delete(linkPath);
        }
        catch
        {
            // 删不掉就留着，不影响卸载结果
        }
    }

    public static string DesktopLink => Path.Combine(ProductInfo.DesktopDir, ProductInfo.ShortcutName + ".lnk");

    public static string StartMenuLink => Path.Combine(ProductInfo.StartMenuDir, ProductInfo.ShortcutName + ".lnk");
}

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace StartRide.Setup;

internal static class ShortcutFactory
{
    private const int CLSID_Size = 0;

    public static bool TryCreate(string linkPath, string targetPath, string workingDir,
                                 string description, string iconPath)
    {
        object? shell = null;
        try
        {

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
        }
    }

    public static string DesktopLink => Path.Combine(ProductInfo.DesktopDir, ProductInfo.ShortcutName + ".lnk");

    public static string StartMenuLink => Path.Combine(ProductInfo.StartMenuDir, ProductInfo.ShortcutName + ".lnk");
}

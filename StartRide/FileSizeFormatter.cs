using System;
using System.Globalization;

namespace StartRide.Core
{
    /// <summary>把字节数说成人话（KB/MB/GB）。项目里没有现成的，放这里给诊断包等界面用。</summary>
    public static class FileSizeFormatter
    {
        public static string Format(long bytes)
        {
            if (bytes < 0)
            {
                bytes = 0;
            }
            if (bytes < 1024)
            {
                return bytes.ToString(CultureInfo.CurrentCulture) + " B";
            }
            double kb = bytes / 1024.0;
            if (kb < 1024)
            {
                return kb.ToString("0.#", CultureInfo.CurrentCulture) + " KB";
            }
            double mb = kb / 1024.0;
            if (mb < 1024)
            {
                return mb.ToString("0.#", CultureInfo.CurrentCulture) + " MB";
            }
            return (mb / 1024.0).ToString("0.##", CultureInfo.CurrentCulture) + " GB";
        }
    }
}

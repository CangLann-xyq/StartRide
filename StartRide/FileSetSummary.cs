using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StartRide.Core
{
    /// <summary>
    /// 详情页顶部统计条的算数。三个页面（车辆管理 / 模组仓库 / 回放）共用，
    /// 免得每处各写一遍"合计多少 GB、平均多少 MB"。
    /// </summary>
    public sealed class FileSetSummary
    {
        public int Count { get; private init; }

        public long TotalBytes { get; private init; }

        public long AverageBytes { get; private init; }

        public long MaxBytes { get; private init; }

        public long MinBytes { get; private init; }

        /// <summary>最大的那一项的名字（没有则为空）。</summary>
        public string MaxName { get; private init; } = "";

        public string CountText => Count.ToString("N0", CultureInfo.CurrentCulture);

        public string TotalText => FileSizeFormatter.Format(TotalBytes);

        public string AverageText => FileSizeFormatter.Format(AverageBytes);

        public string MaxText => FileSizeFormatter.Format(MaxBytes);

        public string MinText => FileSizeFormatter.Format(MinBytes);

        public bool IsEmpty => Count == 0;

        /// <summary>
        /// 按体积分档计数。BeamNG 的车辆/模组 zip 体积跨 4 个数量级，
        /// 分档比只给一个总数更能看出"哪几个是大头"。
        /// </summary>
        public int LargeCount { get; private init; }

        public int MediumCount { get; private init; }

        public int SmallCount { get; private init; }

        public static FileSetSummary From(IEnumerable<(string Name, long Bytes)> items)
        {
            var list = items?.ToList() ?? new List<(string Name, long Bytes)>();
            if (list.Count == 0)
            {
                return new FileSetSummary();
            }

            long total = 0, max = 0, min = long.MaxValue;
            string maxName = "";
            int large = 0, medium = 0, small = 0;

            foreach ((string name, long bytes) in list)
            {
                long b = bytes < 0 ? 0 : bytes;
                total += b;
                if (b > max)
                {
                    max = b;
                    maxName = name ?? "";
                }
                if (b < min) min = b;

                if (b >= 200L * 1024 * 1024) large++;
                else if (b >= 20L * 1024 * 1024) medium++;
                else small++;
            }

            return new FileSetSummary
            {
                Count = list.Count,
                TotalBytes = total,
                AverageBytes = total / list.Count,
                MaxBytes = max,
                MinBytes = min == long.MaxValue ? 0 : min,
                MaxName = maxName,
                LargeCount = large,
                MediumCount = medium,
                SmallCount = small,
            };
        }
    }

    /// <summary>体积分档的中文说法，界面直接绑。</summary>
    public static class SizeBands
    {
        public const string Large = "大型包 ≥ 200 MB";
        public const string Medium = "中型包 20–200 MB";
        public const string Small = "小型包 < 20 MB";
    }
}

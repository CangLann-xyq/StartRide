using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StartRide.Core
{

    public sealed class FileSetSummary
    {
        public int Count { get; private init; }

        public long TotalBytes { get; private init; }

        public long AverageBytes { get; private init; }

        public long MaxBytes { get; private init; }

        public long MinBytes { get; private init; }

        public string MaxName { get; private init; } = "";

        public string CountText => Count.ToString("N0", CultureInfo.CurrentCulture);

        public string TotalText => FileSizeFormatter.Format(TotalBytes);

        public string AverageText => FileSizeFormatter.Format(AverageBytes);

        public string MaxText => FileSizeFormatter.Format(MaxBytes);

        public string MinText => FileSizeFormatter.Format(MinBytes);

        public bool IsEmpty => Count == 0;

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

    public static class SizeBands
    {
        public const string Large = "大型包 ≥ 200 MB";
        public const string Medium = "中型包 20–200 MB";
        public const string Small = "小型包 < 20 MB";
    }
}

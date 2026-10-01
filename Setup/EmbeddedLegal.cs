using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace StartRide.Setup;

/// <summary>
/// 安装向导内嵌的法律文档：读取嵌入资源 + 轻量 Markdown 渲染。
/// 资源名必须与 StartRide.Setup.csproj 里的 LogicalName 一致。
/// </summary>
internal static class EmbeddedLegal
{
    private const string Prefix = "StartRide.Setup.Resources.Legal.";

    internal const string UserAgreement = Prefix + "01-user-agreement.md";

    internal const string PrivacyPolicy = Prefix + "02-privacy-policy.md";

    private static readonly Dictionary<string, string> Cache = new(StringComparer.Ordinal);

    private static readonly Regex InlinePattern =
        new(@"(\*\*[^*]+\*\*|`[^`]+`)", RegexOptions.Compiled);

    private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0xEC, 0xF2));

    private static readonly Brush DimBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x93, 0xA3));

    private static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(0x08, 0x91, 0xFE));

    private static readonly Brush LineBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x23, 0x2C));

    internal static string Read(string resourceName)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(resourceName, out string? hit))
            {
                return hit;
            }
        }

        string text = string.Empty;

        try
        {
            using Stream? stream = typeof(EmbeddedLegal).Assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                text = reader.ReadToEnd();
            }
        }
        catch
        {
            text = string.Empty;
        }

        lock (Cache)
        {
            Cache[resourceName] = text;
        }

        return text;
    }

    /// <summary>
    /// 把 Markdown 渲染成 FlowDocument。found 为 false 表示资源缺失。
    /// </summary>
    internal static FlowDocument Build(string resourceName, out bool found)
    {
        string markdown = Read(resourceName);
        found = !string.IsNullOrWhiteSpace(markdown);

        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            FontSize = 13,
            Foreground = TextBrush,
            Background = Brushes.Transparent,
            PagePadding = new Thickness(30, 24, 30, 32),
            LineHeight = 21,
        };

        if (!found)
        {
            doc.Blocks.Add(new Paragraph(new Run("（文档内容缺失，请到官网 https://startride.top 查看）")
            {
                Foreground = DimBrush,
            }));
            return doc;
        }

        var body = new List<string>();
        var quote = new List<string>();

        void FlushBody()
        {
            if (body.Count == 0)
            {
                return;
            }

            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 9) };
            AddInlines(paragraph, JoinSoftLines(body));
            doc.Blocks.Add(paragraph);
            body.Clear();
        }

        void FlushQuote()
        {
            if (quote.Count == 0)
            {
                return;
            }

            var paragraph = new Paragraph
            {
                Margin = new Thickness(16, 2, 0, 10),
                FontSize = 12.5,
                Foreground = DimBrush,
                Padding = new Thickness(12, 0, 0, 0),
                BorderBrush = LineBrush,
                BorderThickness = new Thickness(2, 0, 0, 0),
            };

            AddInlines(paragraph, JoinSoftLines(quote));
            doc.Blocks.Add(paragraph);
            quote.Clear();
        }

        void FlushAll()
        {
            FlushBody();
            FlushQuote();
        }

        foreach (string raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.Trim();

            if (line.Length == 0)
            {
                FlushAll();
                continue;
            }

            if (IsRule(line))
            {
                FlushAll();
                doc.Blocks.Add(new Paragraph(new Run(string.Empty)) { Margin = new Thickness(0, 4, 0, 4) });
                continue;
            }

            if (line.StartsWith("### ", StringComparison.Ordinal)
                || line.StartsWith("## ", StringComparison.Ordinal)
                || line.StartsWith("# ", StringComparison.Ordinal))
            {
                FlushAll();

                int level = line.StartsWith("### ", StringComparison.Ordinal) ? 3
                    : line.StartsWith("## ", StringComparison.Ordinal) ? 2 : 1;

                double size = level == 1 ? 19 : (level == 2 ? 15 : 13.5);
                var margin = level == 1 ? new Thickness(0, 2, 0, 10)
                    : new Thickness(0, level == 2 ? 16 : 12, 0, level == 2 ? 6 : 4);

                var heading = new Paragraph
                {
                    FontSize = size,
                    FontWeight = FontWeights.SemiBold,
                    Margin = margin,
                };

                AddInlines(heading, line.Substring(level + 1).Trim());
                doc.Blocks.Add(heading);
                continue;
            }

            if (line.StartsWith("> ", StringComparison.Ordinal))
            {
                FlushBody();
                quote.Add(line.Substring(2).Trim());
                continue;
            }

            if (line == ">")
            {
                FlushBody();
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal)
                || line.StartsWith("* ", StringComparison.Ordinal)
                || line.StartsWith("+ ", StringComparison.Ordinal)
                || line.StartsWith("• ", StringComparison.Ordinal))
            {
                FlushAll();

                var item = new Paragraph { Margin = new Thickness(18, 1, 0, 3) };
                item.Inlines.Add(new Run("•  ") { Foreground = AccentBrush });
                AddInlines(item, line.Substring(2).Trim());
                doc.Blocks.Add(item);
                continue;
            }

            if (line.StartsWith("|", StringComparison.Ordinal))
            {
                FlushAll();

                if (IsTableSeparator(line))
                {
                    continue;
                }

                var row = new Paragraph
                {
                    Margin = new Thickness(0, 1, 0, 1),
                    FontSize = 12.5,
                    Foreground = DimBrush,
                };

                var cells = new List<string>();
                foreach (string cell in line.Trim('|').Split('|'))
                {
                    cells.Add(cell.Trim());
                }

                AddInlines(row, string.Join("    ｜    ", cells));
                doc.Blocks.Add(row);
                continue;
            }

            FlushQuote();
            body.Add(line);
        }

        FlushAll();
        return doc;
    }

    /// <summary>
    /// 把同一段落内的软换行拼成一行。
    /// </summary>
    /// <remarks>
    /// 必须做这一步：Markdown 的 `**加粗**` 允许跨行写，而行内解析是按行跑的 ——
    /// 不合并的话 `**` 会原样显示在界面上（实拍到了：隐私政策开头那段"你勾选同意…"）。
    /// </remarks>
    private static string JoinSoftLines(List<string> lines)
    {
        if (lines.Count == 1)
        {
            return lines[0];
        }

        var sb = new StringBuilder();

        foreach (string line in lines)
        {
            if (sb.Length > 0 && line.Length > 0)
            {
                char prev = sb[sb.Length - 1];
                char next = line[0];

                if (!IsWide(prev) && !IsWide(next) && prev != ' ')
                {
                    sb.Append(' ');
                }
            }

            sb.Append(line);
        }

        return sb.ToString();
    }

    private static bool IsWide(char c)
        => (c >= 0x2E80 && c <= 0x9FFF)
           || (c >= 0xF900 && c <= 0xFAFF)
           || (c >= 0xFF00 && c <= 0xFFEF);

    private static void AddInlines(Paragraph paragraph, string text)
    {
        int cursor = 0;

        foreach (Match match in InlinePattern.Matches(text))
        {
            if (match.Index > cursor)
            {
                paragraph.Inlines.Add(new Run(text.Substring(cursor, match.Index - cursor)));
            }

            string token = match.Value;

            if (token.StartsWith("**", StringComparison.Ordinal))
            {
                paragraph.Inlines.Add(new Bold(new Run(token.Substring(2, token.Length - 4))));
            }
            else
            {
                paragraph.Inlines.Add(new Run(token.Substring(1, token.Length - 2))
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                });
            }

            cursor = match.Index + match.Length;
        }

        if (cursor < text.Length)
        {
            paragraph.Inlines.Add(new Run(text.Substring(cursor)));
        }
    }

    private static bool IsRule(string line)
    {
        if (line.Length < 3)
        {
            return false;
        }

        foreach (char c in line)
        {
            if (c != '-' && c != '*' && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTableSeparator(string line)
    {
        foreach (char c in line)
        {
            if (c != '|' && c != '-' && c != ':' && c != ' ')
            {
                return false;
            }
        }

        return line.IndexOf('-') >= 0;
    }
}

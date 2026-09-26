using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace StartRide.App.Markdown;

/// <summary>
/// 把法律文书用的那部分 Markdown 切成块。
///
/// 只实现条款正文真正用到的语法，刻意不做通用实现：
///   标题（# ~ ######）· 段落（含折行续写）· 有序/无序列表（一层嵌套）·
///   引用（&gt;）· 代码块（```）· 表格（| … |）· 分隔线（---）
/// 行内记号（**加粗** / *斜体* / `行内代码`）原样保留在 <see cref="MarkdownBlock.Text"/> 里，
/// 由渲染层的 <c>MarkdownText</c> 附加属性处理。
/// </summary>
public static class MarkdownParser
{
	private static readonly Regex HeadingPattern = new(@"^(#{1,6})\s+(.*)$", RegexOptions.Compiled);

	// 无序：- * + ；有序：1. / 1)
	private static readonly Regex ListPattern = new(@"^(\s*)([-*+]|\d{1,3}[.)])\s+(.*)$", RegexOptions.Compiled);

	private static readonly Regex DividerPattern = new(@"^\s{0,3}(?:-{3,}|\*{3,}|_{3,})\s*$", RegexOptions.Compiled);

	public static IReadOnlyList<MarkdownBlock> Parse(string? markdown)
	{
		var blocks = new List<MarkdownBlock>();
		if (string.IsNullOrWhiteSpace(markdown))
		{
			return blocks;
		}

		string text = markdown!.Replace("\r\n", "\n").Replace('\r', '\n');
		if (text.Length > 0 && text[0] == '\uFEFF')
		{
			text = text.Substring(1);
		}

		string[] lines = text.Split('\n');

		var paragraph = new List<string>();
		var quote = new List<string>();
		var code = new List<string>();
		bool inCode = false;
		bool inList = false;
		bool lastWasListItem = false;

		void FlushParagraph()
		{
			if (paragraph.Count == 0)
			{
				return;
			}
			blocks.Add(MarkdownBlock.Paragraph(string.Join(" ", paragraph)));
			paragraph.Clear();
		}

		void FlushQuote()
		{
			if (quote.Count == 0)
			{
				return;
			}
			blocks.Add(MarkdownBlock.Quote(string.Join("\n", quote)));
			quote.Clear();
		}

		void FlushAll()
		{
			FlushParagraph();
			FlushQuote();
			inList = false;
			lastWasListItem = false;
		}

		for (int i = 0; i < lines.Length; i++)
		{
			string raw = lines[i];
			string line = raw.TrimEnd();

			if (inCode)
			{
				if (line.StartsWith("```", StringComparison.Ordinal))
				{
					inCode = false;
					blocks.Add(MarkdownBlock.Code(code.ToArray()));
					code.Clear();
				}
				else
				{
					code.Add(raw.TrimEnd());
				}
				continue;
			}

			if (line.StartsWith("```", StringComparison.Ordinal))
			{
				FlushAll();
				inCode = true;
				continue;
			}

			if (line.Trim().Length == 0)
			{
				FlushAll();
				continue;
			}

			string head = line.TrimStart();

			if (DividerPattern.IsMatch(line))
			{
				FlushAll();
				blocks.Add(MarkdownBlock.Divider());
				continue;
			}

			Match heading = HeadingPattern.Match(line);
			if (heading.Success)
			{
				FlushAll();
				blocks.Add(MarkdownBlock.Heading(heading.Groups[1].Value.Length, heading.Groups[2].Value.Trim()));
				continue;
			}

			if (head.StartsWith(">", StringComparison.Ordinal))
			{
				FlushParagraph();
				inList = false;
				lastWasListItem = false;
				quote.Add(head.Substring(1).TrimStart());
				continue;
			}

			if (head.StartsWith("|", StringComparison.Ordinal))
			{
				FlushAll();
				var rows = new List<MarkdownTableRow>();
				int j = i;
				while (j < lines.Length && lines[j].TrimStart().StartsWith("|", StringComparison.Ordinal))
				{
					string[] cells = SplitRow(lines[j]);
					if (rows.Count > 0 && IsSeparatorRow(cells))
					{
						j++;
						continue;
					}
					rows.Add(new MarkdownTableRow(rows.Count == 0, cells));
					j++;
				}
				if (rows.Count > 0)
				{
					blocks.Add(MarkdownBlock.Table(rows));
				}
				i = j - 1;
				continue;
			}

			Match list = ListPattern.Match(raw);
			if (list.Success)
			{
				FlushParagraph();
				int indent = list.Groups[1].Value.Length;
				// 正文里每层缩进 3 个空格；这里向上取整，容忍 1~2 空格的手写缩进。
				int level = Math.Min(3, (indent + 2) / 3);
				string marker = list.Groups[2].Value;
				string content = list.Groups[3].Value.Trim();
				bool numbered = marker.Length > 0 && char.IsDigit(marker[0]);
				blocks.Add(numbered
					? MarkdownBlock.Numbered(level, marker.EndsWith(".", StringComparison.Ordinal) ? marker : marker + ".", content)
					: MarkdownBlock.Bullet(level, content));
				inList = true;
				lastWasListItem = true;
				continue;
			}

			// 列表项的折行续写：缩进开头、又不带标记 → 并回上一项。
			if (inList && lastWasListItem && raw.Length > 0 && char.IsWhiteSpace(raw[0]) && blocks.Count > 0)
			{
				MarkdownBlock last = blocks[blocks.Count - 1];
				last.Text = last.Text + " " + line.Trim();
				continue;
			}

			FlushQuote();
			inList = false;
			lastWasListItem = false;
			paragraph.Add(line.Trim());
		}

		if (inCode)
		{
			blocks.Add(MarkdownBlock.Code(code.ToArray()));
			code.Clear();
		}
		FlushAll();
		return blocks;
	}

	private static string[] SplitRow(string line)
	{
		string trimmed = line.Trim();
		if (trimmed.StartsWith("|", StringComparison.Ordinal))
		{
			trimmed = trimmed.Substring(1);
		}
		if (trimmed.EndsWith("|", StringComparison.Ordinal))
		{
			trimmed = trimmed.Substring(0, trimmed.Length - 1);
		}
		string[] parts = trimmed.Split('|');
		var cells = new string[parts.Length];
		for (int i = 0; i < parts.Length; i++)
		{
			cells[i] = parts[i].Trim();
		}
		return cells;
	}

	/// <summary>表格的第二行是 <c>|---|---|</c> 这样的分隔行，不当作数据。</summary>
	private static bool IsSeparatorRow(string[] cells)
	{
		if (cells.Length == 0)
		{
			return false;
		}
		foreach (string cell in cells)
		{
			if (cell.Length == 0)
			{
				return false;
			}
			foreach (char ch in cell)
			{
				if (ch != '-' && ch != ':' && ch != ' ')
				{
					return false;
				}
			}
		}
		return true;
	}
}

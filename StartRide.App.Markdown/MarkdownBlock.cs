using System;
using System.Collections.Generic;
using System.Windows;

namespace StartRide.App.Markdown;

/// <summary>正文块的种类。决定界面上用哪个 <c>DataTemplate</c> 渲染。</summary>
public enum MarkdownBlockKind
{
	Heading,
	Paragraph,
	Bullet,
	Numbered,
	Quote,
	Code,
	Divider,
	Table
}

/// <summary>表格里的一行。<see cref="Cell0"/>～<see cref="Cell2"/> 是给固定三列模板用的取位器。</summary>
public sealed class MarkdownTableRow
{
	public MarkdownTableRow(bool isHeader, IReadOnlyList<string> cells)
	{
		IsHeader = isHeader;
		Cells = cells;
	}

	public bool IsHeader { get; }

	public IReadOnlyList<string> Cells { get; }

	public string Cell0 => Cells.Count > 0 ? Cells[0] : string.Empty;

	public string Cell1 => Cells.Count > 1 ? Cells[1] : string.Empty;

	public string Cell2 => Cells.Count > 2 ? Cells[2] : string.Empty;
}

/// <summary>
/// 一段已经按块切分好的 Markdown 正文。
///
/// 为什么自己解析而不是引第三方库：条款正文是本项目自己的七份 md，用到的语法就
/// 标题 / 段落 / 有序无序列表 / 引用 / 代码块 / 表格 / 分隔线这几种，没必要为它拉一个
/// 完整实现（还要额外处理它的渲染后端）。块级在 <see cref="MarkdownParser"/> 里切，
/// 行内（加粗、斜体、行内代码）交给 <c>MarkdownText</c> 这个附加属性。
/// </summary>
public sealed class MarkdownBlock
{
	private MarkdownBlock(MarkdownBlockKind kind, int level, string text, string marker, IReadOnlyList<string> lines, IReadOnlyList<MarkdownTableRow> rows)
	{
		Kind = kind;
		Level = level;
		Text = text;
		Marker = marker;
		Lines = lines;
		Rows = rows;
	}

	public MarkdownBlockKind Kind { get; }

	/// <summary>标题层级（1-6）或列表嵌套深度（0 起）。</summary>
	public int Level { get; }

	/// <summary>内联 Markdown 原文（加粗等记号保留，交给渲染层处理）。</summary>
	public string Text { get; internal set; }

	/// <summary>列表的项目符号 / 序号；其它块为空。</summary>
	public string Marker { get; }

	/// <summary>代码块的行；其它块为空。</summary>
	public IReadOnlyList<string> Lines { get; }

	/// <summary>表格行；其它块为空。</summary>
	public IReadOnlyList<MarkdownTableRow> Rows { get; }

	/// <summary>代码块的整段文本（渲染层直接喂给 <c>TextBlock.Text</c>）。</summary>
	public string CodeText => Lines.Count == 0 ? string.Empty : string.Join(Environment.NewLine, Lines);

	/// <summary>数据模板的键（与阅读器 XAML 里的模板一一对应）。</summary>
	public string TemplateKey => Kind switch
	{
		MarkdownBlockKind.Heading => "h" + Math.Clamp(Level, 1, 6),
		MarkdownBlockKind.Paragraph => "p",
		MarkdownBlockKind.Bullet => "bullet",
		MarkdownBlockKind.Numbered => "number",
		MarkdownBlockKind.Quote => "quote",
		MarkdownBlockKind.Code => "code",
		MarkdownBlockKind.Divider => "divider",
		MarkdownBlockKind.Table => Rows.Count > 0 && Rows[0].Cells.Count >= 3 ? "table3" : "table",
		_ => "p"
	};

	/// <summary>列表按嵌套层级做左缩进（顺带带上条目间的间距）；其它块不缩进。</summary>
	public Thickness IndentMargin =>
		Kind == MarkdownBlockKind.Bullet || Kind == MarkdownBlockKind.Numbered
			? new Thickness(Level * 18, 0, 0, 8)
			: default;

	public static MarkdownBlock Heading(int level, string text)
		=> new(MarkdownBlockKind.Heading, level, text, string.Empty, Array.Empty<string>(), Array.Empty<MarkdownTableRow>());

	public static MarkdownBlock Paragraph(string text)
		=> new(MarkdownBlockKind.Paragraph, 0, text, string.Empty, Array.Empty<string>(), Array.Empty<MarkdownTableRow>());

	public static MarkdownBlock Bullet(int level, string text)
		=> new(MarkdownBlockKind.Bullet, level, text, "•", Array.Empty<string>(), Array.Empty<MarkdownTableRow>());

	public static MarkdownBlock Numbered(int level, string marker, string text)
		=> new(MarkdownBlockKind.Numbered, level, text, marker, Array.Empty<string>(), Array.Empty<MarkdownTableRow>());

	public static MarkdownBlock Quote(string text)
		=> new(MarkdownBlockKind.Quote, 0, text, string.Empty, Array.Empty<string>(), Array.Empty<MarkdownTableRow>());

	public static MarkdownBlock Code(IReadOnlyList<string> lines)
		=> new(MarkdownBlockKind.Code, 0, string.Empty, string.Empty, lines, Array.Empty<MarkdownTableRow>());

	public static MarkdownBlock Divider()
		=> new(MarkdownBlockKind.Divider, 0, string.Empty, string.Empty, Array.Empty<string>(), Array.Empty<MarkdownTableRow>());

	public static MarkdownBlock Table(IReadOnlyList<MarkdownTableRow> rows)
		=> new(MarkdownBlockKind.Table, 0, string.Empty, string.Empty, Array.Empty<string>(), rows);
}

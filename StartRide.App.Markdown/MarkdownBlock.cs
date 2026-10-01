using System;
using System.Collections.Generic;
using System.Windows;

namespace StartRide.App.Markdown;

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

	public int Level { get; }

	public string Text { get; internal set; }

	public string Marker { get; }

	public IReadOnlyList<string> Lines { get; }

	public IReadOnlyList<MarkdownTableRow> Rows { get; }

	public string CodeText => Lines.Count == 0 ? string.Empty : string.Join(Environment.NewLine, Lines);

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

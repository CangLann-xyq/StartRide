using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace StartRide.App.Controls;

/// <summary>
/// 往 <see cref="TextBlock"/> 的 <c>Inlines</c> 里填**行内 Markdown**。
///
/// WPF 的 <c>Inlines</c> 不是依赖属性、没法直接绑定，所以走附加属性：
/// XAML 里写 <c>controls:MarkdownText.Text="{Binding Text}"</c>，赋值时把文本切成
/// Run / Bold / Italic / Hyperlink 塞进 <c>Inlines</c>。
///
/// 支持：**加粗** · *斜体* / _斜体_ · `行内代码` · [文字](链接)
/// 其余字符原样输出 —— 法律文书里出现的反斜杠、引号等不需要转义处理。
/// </summary>
public static class MarkdownText
{
	private static readonly Regex TokenPattern = new(
		@"(?<bold>\*\*(?<boldText>.+?)\*\*)|(?<italic>\*(?<italicText>[^*\n]+?)\*)|(?<italicAlt>_(?<italicAltText>[^_\n]+?)_)|(?<code>`(?<codeText>[^`\n]+?)`)|(?<link>\[(?<linkText>[^\]]+?)\]\((?<linkUrl>[^)\s]+)\))",
		RegexOptions.Compiled);

	private static readonly FontFamily MonoFont = new("Cascadia Mono,Consolas,Courier New");

	public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
		"Text", typeof(string), typeof(MarkdownText), new PropertyMetadata(string.Empty, OnAnyChanged));

	public static readonly DependencyProperty CodeForegroundProperty = DependencyProperty.RegisterAttached(
		"CodeForeground", typeof(Brush), typeof(MarkdownText), new PropertyMetadata(null, OnAnyChanged));

	public static readonly DependencyProperty CodeBackgroundProperty = DependencyProperty.RegisterAttached(
		"CodeBackground", typeof(Brush), typeof(MarkdownText), new PropertyMetadata(null, OnAnyChanged));

	public static string GetText(DependencyObject element) => (string)element.GetValue(TextProperty);

	public static void SetText(DependencyObject element, string value) => element.SetValue(TextProperty, value);

	public static Brush? GetCodeForeground(DependencyObject element) => (Brush?)element.GetValue(CodeForegroundProperty);

	public static void SetCodeForeground(DependencyObject element, Brush? value) => element.SetValue(CodeForegroundProperty, value);

	public static Brush? GetCodeBackground(DependencyObject element) => (Brush?)element.GetValue(CodeBackgroundProperty);

	public static void SetCodeBackground(DependencyObject element, Brush? value) => element.SetValue(CodeBackgroundProperty, value);

	private static void OnAnyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not TextBlock textBlock)
		{
			return;
		}
		textBlock.Inlines.Clear();
		Append(textBlock.Inlines, GetText(textBlock), GetCodeForeground(textBlock), GetCodeBackground(textBlock));
	}

	public static void Append(InlineCollection target, string? text, Brush? codeForeground, Brush? codeBackground)
	{
		if (target is null)
		{
			return;
		}
		if (string.IsNullOrEmpty(text))
		{
			return;
		}

		int position = 0;
		foreach (Match match in TokenPattern.Matches(text!))
		{
			if (match.Index > position)
			{
				target.Add(new Run(text.Substring(position, match.Index - position)));
			}

			if (match.Groups["bold"].Success)
			{
				target.Add(new Bold(new Run(match.Groups["boldText"].Value)));
			}
			else if (match.Groups["italic"].Success)
			{
				target.Add(new Italic(new Run(match.Groups["italicText"].Value)));
			}
			else if (match.Groups["italicAlt"].Success)
			{
				target.Add(new Italic(new Run(match.Groups["italicAltText"].Value)));
			}
			else if (match.Groups["code"].Success)
			{
				var run = new Run(match.Groups["codeText"].Value) { FontFamily = MonoFont, FontSize = 12.2 };
				if (codeForeground is not null)
				{
					run.Foreground = codeForeground;
				}
				if (codeBackground is not null)
				{
					run.Background = codeBackground;
				}
				target.Add(run);
			}
			else if (match.Groups["link"].Success)
			{
				target.Add(CreateHyperlink(match.Groups["linkText"].Value, match.Groups["linkUrl"].Value));
			}

			position = match.Index + match.Length;
		}

		if (position < text.Length)
		{
			target.Add(new Run(text.Substring(position)));
		}
	}

	private static Hyperlink CreateHyperlink(string label, string url)
	{
		var hyperlink = new Hyperlink(new Run(label)) { ToolTip = url };
		if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
		{
			hyperlink.NavigateUri = uri;
			hyperlink.RequestNavigate += OnRequestNavigate;
		}
		return hyperlink;
	}

	private static void OnRequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
	{
		try
		{
			Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
		}
		catch (Exception)
		{
			// 打不开浏览器不该把阅读器带崩；链接本来只是正文里的补充信息。
		}
		e.Handled = true;
	}
}

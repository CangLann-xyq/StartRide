using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Markdown;
using StartRide.App.Models;
using StartRide.Core;

namespace StartRide.App.ViewModels.Shell;

/// <summary>
/// 内置的条款阅读器。
///
/// 2.11.0 起，法律文书不再跳到浏览器或第三方文档站：正文随包内嵌，
/// 点「查看」直接在这里打开 —— 离线可读，国内也一定打得开。
///
/// 界面上是"目录 + 正文"两栏：左边列全部八条入口，右边渲染当前选中那份的正文。
/// 首次运行弹窗与设置页的「查看」按钮都调 <see cref="OpenCommand"/>，
/// 只是传入的条目不同。
///
/// ⚠️ <see cref="SelectedDocument"/> 必须是**可写**属性：左边的 ListBox 会把它当 TwoWay 绑。
/// 只读属性（哪怕有 private set）会在绑定挂上的那一刻抛 InvalidOperationException，
/// 而这个面板即使 <see cref="IsOpen"/> 为 false 也已经在视觉树里（DialogHost 的内容一直存在），
/// 于是异常发生在**启动阶段** —— 表现是启动器根本起不来，日志里只有一句
/// "无法对…只读属性…进行 TwoWay 绑定"。
/// </summary>
public sealed class LegalReaderViewModel : ObservableObject
{
	private bool isOpen;

	private LegalDocumentItem? selectedDocument;

	private string title = string.Empty;

	private string description = string.Empty;

	private string missingContentHint = string.Empty;

	private IReadOnlyList<MarkdownBlock> blocks = Array.Empty<MarkdownBlock>();

	private RelayCommand<LegalDocumentItem?>? openCommand;

	private RelayCommand? closeCommand;

	/// <summary>全部条目（目录栏显示的顺序就是清单顺序）。</summary>
	public IReadOnlyList<LegalDocumentItem> Documents { get; } = LegalDocumentItemFactory.CreateAll();

	public bool IsOpen
	{
		get => isOpen;
		set
		{
			if (isOpen != value)
			{
				isOpen = value;
				OnPropertyChanged(nameof(IsOpen));
			}
		}
	}

	/// <summary>当前选中的条目。写它就会换正文，所以目录栏点一下就够用。</summary>
	public LegalDocumentItem? SelectedDocument
	{
		get => selectedDocument;
		set
		{
			if (Equals(selectedDocument, value))
			{
				return;
			}

			selectedDocument = value;
			OnPropertyChanged(nameof(SelectedDocument));
			LoadContent(value);
		}
	}

	public string Title
	{
		get => title;
		private set
		{
			if (!string.Equals(title, value, StringComparison.Ordinal))
			{
				title = value;
				OnPropertyChanged(nameof(Title));
			}
		}
	}

	public string Description
	{
		get => description;
		private set
		{
			if (!string.Equals(description, value, StringComparison.Ordinal))
			{
				description = value;
				OnPropertyChanged(nameof(Description));
			}
		}
	}

	/// <summary>正文读不到时给用户的说明（正常情况下是空串）。</summary>
	public string MissingContentHint
	{
		get => missingContentHint;
		private set
		{
			if (!string.Equals(missingContentHint, value, StringComparison.Ordinal))
			{
				missingContentHint = value;
				OnPropertyChanged(nameof(MissingContentHint));
			}
		}
	}

	public IReadOnlyList<MarkdownBlock> Blocks
	{
		get => blocks;
		private set
		{
			blocks = value;
			OnPropertyChanged(nameof(Blocks));
		}
	}

	/// <summary>打开阅读器；<paramref name="document"/> 为 null 时落到第一份。</summary>
	public IRelayCommand<LegalDocumentItem?> OpenCommand => openCommand ??= new RelayCommand<LegalDocumentItem?>(Open);

	public IRelayCommand CloseCommand => closeCommand ??= new RelayCommand(Close);

	public void Open(LegalDocumentItem? document)
	{
		LegalDocumentItem? target = document;
		if (target is null && Documents.Count > 0)
		{
			target = Documents[0];
		}

		SelectedDocument = target;

		// 关掉再打开同一份时 SelectedDocument 没变，setter 里的换正文不会跑；
		// 但内容还在，不需要重算。这里只是保证"被清空过"的情况下也能恢复。
		if (target is not null && Blocks.Count == 0 && string.IsNullOrEmpty(MissingContentHint))
		{
			LoadContent(target);
		}

		IsOpen = true;
	}

	public void Close()
	{
		IsOpen = false;
	}

	private void LoadContent(LegalDocumentItem? document)
	{
		if (document is null)
		{
			Title = string.Empty;
			Description = string.Empty;
			MissingContentHint = string.Empty;
			Blocks = Array.Empty<MarkdownBlock>();
			return;
		}

		Title = document.Title;
		Description = document.Description;

		LegalDocument? definition = LegalDocuments.Find(document.Id);
		string markdown = LegalDocumentContent.Load(definition);
		if (string.IsNullOrWhiteSpace(markdown))
		{
			Blocks = Array.Empty<MarkdownBlock>();
			MissingContentHint = StartRide.App.Resources.Strings.Legal_Reader_MissingContent;
			return;
		}

		MissingContentHint = string.Empty;
		Blocks = SkipDocumentTitle(MarkdownParser.Parse(markdown));
	}

	/// <summary>
	/// 去掉正文开头的那个一级标题。
	///
	/// 七份 md 的第一行都是 <c># 《…》</c>，而阅读器的头部已经把这个标题显示了一次 ——
	/// 不去掉的话正文第一行就是标题的重复。
	/// </summary>
	private static IReadOnlyList<MarkdownBlock> SkipDocumentTitle(IReadOnlyList<MarkdownBlock> parsed)
	{
		if (parsed.Count == 0)
		{
			return parsed;
		}

		MarkdownBlock first = parsed[0];
		if (first.Kind != MarkdownBlockKind.Heading || first.Level != 1)
		{
			return parsed;
		}

		var rest = new List<MarkdownBlock>(parsed.Count - 1);
		for (int i = 1; i < parsed.Count; i++)
		{
			rest.Add(parsed[i]);
		}
		return rest;
	}
}

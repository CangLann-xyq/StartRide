using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StartRide.App.Markdown;
using StartRide.App.Models;
using StartRide.Core;

namespace StartRide.App.ViewModels.Shell;

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

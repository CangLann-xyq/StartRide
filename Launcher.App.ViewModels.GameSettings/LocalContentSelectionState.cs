using System;
using System.Collections.Generic;
using System.Linq;

namespace Launcher.App.ViewModels.GameSettings;

internal sealed class LocalContentSelectionState<TItem>
{
	private readonly Func<TItem, string> pathSelector;

	private readonly Func<TItem, bool> isSelectedSelector;

	private readonly Action<TItem, bool> setSelected;

	private readonly Dictionary<string, TItem> itemsByPath = new Dictionary<string, TItem>(StringComparer.OrdinalIgnoreCase);

	private readonly HashSet<string> selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public IDictionary<string, TItem> ItemsByPath => itemsByPath;

	public string? LastSingleSelectedPath { get; private set; }

	public LocalContentSelectionState(Func<TItem, string> pathSelector, Func<TItem, bool> isSelectedSelector, Action<TItem, bool> setSelected)
	{
		this.pathSelector = pathSelector;
		this.isSelectedSelector = isSelectedSelector;
		this.setSelected = setSelected;
	}

	public void RememberSingleSelection(TItem? selectedItem)
	{
		if (selectedItem != null)
		{
			LastSingleSelectedPath = pathSelector(selectedItem);
		}
	}

	public void ClearCache()
	{
		itemsByPath.Clear();
	}

	public void Reset()
	{
		LastSingleSelectedPath = null;
		selectedPaths.Clear();
	}

	public void BeginMultiSelect(TItem? selectedItem, IReadOnlyList<TItem> visibleItems)
	{
		RememberSingleSelection(selectedItem);
		selectedPaths.Clear();
		ClearVisibleSelections(visibleItems);
	}

	public void ClearSelectedPaths()
	{
		selectedPaths.Clear();
	}

	public void SelectAll(IReadOnlyList<TItem> visibleItems)
	{
		foreach (TItem visibleItem in visibleItems)
		{
			setSelected(visibleItem, arg2: true);
			selectedPaths.Add(pathSelector(visibleItem));
		}
	}

	public void ClearVisibleSelections(IReadOnlyList<TItem> visibleItems)
	{
		foreach (TItem visibleItem in visibleItems)
		{
			setSelected(visibleItem, arg2: false);
		}
	}

	public void ToggleSelection(TItem item)
	{
		bool flag = !isSelectedSelector(item);
		setSelected(item, flag);
		if (flag)
		{
			selectedPaths.Add(pathSelector(item));
		}
		else
		{
			selectedPaths.Remove(pathSelector(item));
		}
	}

	public void SelectSingle(TItem item, IReadOnlyList<TItem> visibleItems)
	{
		LastSingleSelectedPath = pathSelector(item);
		ClearVisibleSelections(visibleItems);
	}

	public void SyncSelectionToItems(IReadOnlyList<TItem> visibleItems, bool isMultiSelectMode)
	{
		if (isMultiSelectMode)
		{
			selectedPaths.IntersectWith(visibleItems.Select(pathSelector));
		}
		foreach (TItem value in itemsByPath.Values)
		{
			setSelected(value, isMultiSelectMode && selectedPaths.Contains(pathSelector(value)));
		}
	}

	public IReadOnlyList<TItem> GetSelectedVisibleItems(IReadOnlyList<TItem> visibleItems)
	{
		return visibleItems.Where((TItem item) => selectedPaths.Contains(pathSelector(item))).ToArray();
	}

	public int CountSelectedVisibleItems(IReadOnlyList<TItem> visibleItems)
	{
		return visibleItems.Count(isSelectedSelector);
	}
}

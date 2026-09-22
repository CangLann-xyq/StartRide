using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Launcher.App.Controls;
using Launcher.App.Utilities;

namespace Launcher.App.Behaviors;

public static class VirtualizedListItemStateBehavior
{
	private sealed class ListBoxState
	{
		private readonly HashSet<object> animatedEntranceItems;

		private readonly DispatcherTimer entranceAnimationTimer;

		private ListBox? listBox;

		private ScrollViewer? scrollViewer;

		private bool isStateRefreshQueued;

		private bool isEntranceAnimationPending;

		private int entranceAnimationPassesRemaining;

		private int observedEntranceAnimationToken;

		private int observedScrollResetToken;

		private bool isScrollResetPending;

		private object? hoveredItem;

		public ListBoxState()
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_002d: Expected O, but got Unknown
			animatedEntranceItems = new HashSet<object>(ReferenceEqualityComparer.Instance);
			entranceAnimationTimer = new DispatcherTimer((DispatcherPriority)6)
			{
				Interval = EntranceAnimationPassInterval
			};
			entranceAnimationTimer.Tick += EntranceAnimationTimer_OnTick;
		}

		public void Attach(ListBox target)
		{
			if (listBox != target)
			{
				Detach();
				listBox = target;
				target.Loaded += ListBox_OnLoaded;
				target.Unloaded += ListBox_OnUnloaded;
				target.SelectionChanged += ListBox_OnSelectionChanged;
				target.ItemContainerGenerator.StatusChanged += ItemContainerGenerator_OnStatusChanged;
				AttachScrollViewer();
				QueueScrollReset(GetScrollResetToken((DependencyObject)(object)target));
				TryApplyPendingScrollReset();
				QueueRenderedItemStateRefresh();
			}
		}

		public void Detach()
		{
			entranceAnimationTimer.Stop();
			DetachScrollViewer();
			if (listBox != null)
			{
				listBox.Loaded -= ListBox_OnLoaded;
				listBox.Unloaded -= ListBox_OnUnloaded;
				listBox.SelectionChanged -= ListBox_OnSelectionChanged;
				listBox.ItemContainerGenerator.StatusChanged -= ItemContainerGenerator_OnStatusChanged;
				listBox = null;
				hoveredItem = null;
				animatedEntranceItems.Clear();
			}
		}

		public void QueueEntranceAnimation(int token)
		{
			if (token != observedEntranceAnimationToken)
			{
				observedEntranceAnimationToken = token;
				isEntranceAnimationPending = true;
				entranceAnimationPassesRemaining = 8;
				animatedEntranceItems.Clear();
				entranceAnimationTimer.Start();
				MarkEntrancePendingContainers();
				QueueRenderedItemStateRefresh();
			}
		}

		public void QueueScrollReset(int token)
		{
			if (token > 0 && token != observedScrollResetToken)
			{
				observedScrollResetToken = token;
				isScrollResetPending = true;
				TryApplyPendingScrollReset();
			}
		}

		private void ListBox_OnLoaded(object sender, RoutedEventArgs e)
		{
			AttachScrollViewer();
			TryApplyPendingScrollReset();
			QueueRenderedItemStateRefresh();
			ListBox listBox = this.listBox;
			if (listBox != null)
			{
				QueueEntranceAnimation(GetEntranceAnimationToken((DependencyObject)(object)listBox));
			}
		}

		private void ListBox_OnUnloaded(object sender, RoutedEventArgs e)
		{
			entranceAnimationTimer.Stop();
			DetachScrollViewer();
		}

		private void ListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			QueueRenderedItemStateRefresh();
		}

		private void ItemContainerGenerator_OnStatusChanged(object? sender, EventArgs e)
		{
			ListBox? obj = listBox;
			if (obj != null && obj.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
			{
				MarkEntrancePendingContainers();
				PreparePendingEntranceAnimationStates();
			}
			QueueRenderedItemStateRefresh();
		}

		private void AttachScrollViewer()
		{
			if (listBox == null)
			{
				return;
			}
			listBox.ApplyTemplate();
			ScrollViewer scrollViewer = VisualTreeSearch.FindDescendant((DependencyObject)(object)listBox, (ScrollViewer _) => true);
			if (this.scrollViewer != scrollViewer)
			{
				DetachScrollViewer();
				this.scrollViewer = scrollViewer;
				if (this.scrollViewer != null)
				{
					this.scrollViewer.ScrollChanged += ScrollViewer_OnScrollChanged;
				}
			}
		}

		private void TryApplyPendingScrollReset()
		{
			if (isScrollResetPending && listBox != null)
			{
				AttachScrollViewer();
				if (scrollViewer != null)
				{
					isScrollResetPending = false;
					SmoothScrollBehavior.CancelAnimation(scrollViewer);
					scrollViewer.ScrollToVerticalOffset(0.0);
					QueueRenderedItemStateRefresh();
				}
			}
		}

		private void DetachScrollViewer()
		{
			if (scrollViewer != null)
			{
				scrollViewer.ScrollChanged -= ScrollViewer_OnScrollChanged;
				scrollViewer = null;
			}
		}

		private void ScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			QueueRenderedItemStateRefresh();
		}

		public void QueueRenderedItemStateRefresh()
		{
			if (!isStateRefreshQueued && listBox != null)
			{
				isStateRefreshQueued = true;
				((DispatcherObject)listBox).Dispatcher.BeginInvoke((Delegate)(Action)delegate
				{
					isStateRefreshQueued = false;
					RefreshRenderedItemState();
					PlayPendingEntranceAnimations();
				}, (DispatcherPriority)6, Array.Empty<object>());
			}
		}

		private void EntranceAnimationTimer_OnTick(object? sender, EventArgs e)
		{
			if (!isEntranceAnimationPending)
			{
				entranceAnimationTimer.Stop();
				return;
			}
			QueueRenderedItemStateRefresh();
			entranceAnimationPassesRemaining--;
			if (entranceAnimationPassesRemaining <= 0)
			{
				isEntranceAnimationPending = false;
				ClearEntrancePendingStates();
				entranceAnimationTimer.Stop();
			}
		}

		private void RefreshRenderedItemState()
		{
			if (listBox == null)
			{
				return;
			}
			IReadOnlyList<ListBoxItem> readOnlyList = FindRealizedContainers(listBox);
			for (int i = 0; i < readOnlyList.Count; i++)
			{
				ListBoxItem listBoxItem = readOnlyList[i];
				VirtualizedListItemState virtualizedListItemState = EnsureItemState(listBoxItem);
				UpdateEntrancePendingState(listBoxItem, virtualizedListItemState);
				virtualizedListItemState.IsFirstVisible = i == 0;
				virtualizedListItemState.IsLastVisible = i == readOnlyList.Count - 1;
				virtualizedListItemState.IsPreviousItemHighlighted = i > 0 && IsHighlighted(readOnlyList[i - 1].DataContext);
				ListPageItemButton listPageItemButton = VisualTreeSearch.FindDescendant((DependencyObject)(object)listBoxItem, (ListPageItemButton _) => true);
				if (listPageItemButton != null)
				{
					HookItemButton(listPageItemButton);
				}
			}
		}

		private void PlayPendingEntranceAnimations()
		{
			if (isEntranceAnimationPending && listBox != null)
			{
				if (listBox.Items.Count == 0)
				{
					isEntranceAnimationPending = false;
					ClearEntrancePendingStates();
					entranceAnimationTimer.Stop();
				}
				else
				{
					PreparePendingEntranceAnimationStates();
				}
			}
		}

		private void PreparePendingEntranceAnimationStates()
		{
			if (!isEntranceAnimationPending || listBox == null)
			{
				return;
			}
			foreach (ListBoxItem item in FindRealizedContainers(listBox))
			{
				int num = listBox.ItemContainerGenerator.IndexFromContainer((DependencyObject)(object)item);
				if (num >= 0)
				{
					object dataContext = item.DataContext;
					if (dataContext != null && !animatedEntranceItems.Contains(dataContext) && IsContainerInUsableViewport(item))
					{
						VirtualizedListItemState virtualizedListItemState = EnsureItemState(item);
						virtualizedListItemState.IsEntranceAnimationPending = true;
						virtualizedListItemState.EnterAnimationIndex = num;
						virtualizedListItemState.ShouldPlayEnterAnimation = true;
						animatedEntranceItems.Add(dataContext);
					}
				}
			}
		}

		private void MarkEntrancePendingContainers()
		{
			if (!isEntranceAnimationPending || listBox == null)
			{
				return;
			}
			foreach (ListBoxItem item in FindRealizedContainers(listBox))
			{
				UpdateEntrancePendingState(item, EnsureItemState(item));
			}
		}

		private void UpdateEntrancePendingState(ListBoxItem container, VirtualizedListItemState state)
		{
			state.IsEntranceAnimationPending = ShouldHoldForEntranceAnimation(container);
		}

		private bool ShouldHoldForEntranceAnimation(ListBoxItem container)
		{
			if (!isEntranceAnimationPending || listBox == null)
			{
				return false;
			}
			object dataContext = container.DataContext;
			if (dataContext == null || animatedEntranceItems.Contains(dataContext))
			{
				return false;
			}
			if (listBox.ItemContainerGenerator.IndexFromContainer((DependencyObject)(object)container) >= 0)
			{
				return IsContainerInUsableViewport(container);
			}
			return false;
		}

		private void ClearEntrancePendingStates()
		{
			if (listBox == null)
			{
				return;
			}
			foreach (ListBoxItem item in FindRealizedContainers(listBox))
			{
				EnsureItemState(item).IsEntranceAnimationPending = false;
			}
		}

		private bool IsContainerInUsableViewport(ListBoxItem container)
		{
			//IL_0053: Unknown result type (might be due to invalid IL or missing references)
			//IL_0058: Unknown result type (might be due to invalid IL or missing references)
			//IL_005d: Unknown result type (might be due to invalid IL or missing references)
			if (scrollViewer == null || scrollViewer.ActualHeight <= 0.0 || listBox == null)
			{
				return true;
			}
			try
			{
				Rect val = container.TransformToAncestor(scrollViewer).TransformBounds(new Rect(0.0, 0.0, container.ActualWidth, container.ActualHeight));
				double contentTopOffset = GetContentTopOffset((DependencyObject)(object)listBox);
				double num = Math.Max(contentTopOffset, scrollViewer.ActualHeight);
				return val.Bottom > contentTopOffset && val.Top < num;
			}
			catch (InvalidOperationException)
			{
				return false;
			}
		}

		private static IReadOnlyList<ListBoxItem> FindRealizedContainers(ListBox listBox)
		{
			List<ListBoxItem> list = new List<ListBoxItem>();
			AddRealizedContainers((DependencyObject)(object)listBox, list);
			list.Sort((ListBoxItem left, ListBoxItem right) => listBox.ItemContainerGenerator.IndexFromContainer((DependencyObject)(object)left).CompareTo(listBox.ItemContainerGenerator.IndexFromContainer((DependencyObject)(object)right)));
			return list;
		}

		private static void AddRealizedContainers(DependencyObject root, ICollection<ListBoxItem> containers)
		{
			int childrenCount = VisualTreeHelper.GetChildrenCount(root);
			for (int i = 0; i < childrenCount; i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(root, i);
				if (child is ListBoxItem item)
				{
					containers.Add(item);
				}
				AddRealizedContainers(child, containers);
			}
		}

		private static VirtualizedListItemState EnsureItemState(ListBoxItem container)
		{
			if (container.Tag is VirtualizedListItemState result)
			{
				return result;
			}
			return (VirtualizedListItemState)(container.Tag = new VirtualizedListItemState());
		}

		private void HookItemButton(ListPageItemButton button)
		{
			button.MouseEnter -= ItemButton_OnMouseEnter;
			button.MouseLeave -= ItemButton_OnMouseLeave;
			button.MouseEnter += ItemButton_OnMouseEnter;
			button.MouseLeave += ItemButton_OnMouseLeave;
		}

		private void ItemButton_OnMouseEnter(object sender, MouseEventArgs e)
		{
			if (sender is FrameworkElement { DataContext: { } dataContext })
			{
				hoveredItem = dataContext;
			}
			QueueRenderedItemStateRefresh();
		}

		private void ItemButton_OnMouseLeave(object sender, MouseEventArgs e)
		{
			hoveredItem = null;
			QueueRenderedItemStateRefresh();
		}

		private bool IsHighlighted(object? item)
		{
			if (item != null)
			{
				if (item != hoveredItem)
				{
					return item == listBox?.SelectedItem;
				}
				return true;
			}
			return false;
		}
	}

	private const int EntranceAnimationPassCount = 8;

	private static readonly TimeSpan EntranceAnimationPassInterval;

	public static readonly DependencyProperty IsEnabledProperty;

	public static readonly DependencyProperty EntranceAnimationTokenProperty;

	public static readonly DependencyProperty ScrollResetTokenProperty;

	public static readonly DependencyProperty ContentTopOffsetProperty;

	private static readonly DependencyProperty StateProperty;

	public static bool GetIsEnabled(DependencyObject element)
	{
		return (bool)element.GetValue(IsEnabledProperty);
	}

	public static void SetIsEnabled(DependencyObject element, bool value)
	{
		element.SetValue(IsEnabledProperty, (object)value);
	}

	public static int GetEntranceAnimationToken(DependencyObject element)
	{
		return (int)element.GetValue(EntranceAnimationTokenProperty);
	}

	public static void SetEntranceAnimationToken(DependencyObject element, int value)
	{
		element.SetValue(EntranceAnimationTokenProperty, (object)value);
	}

	public static int GetScrollResetToken(DependencyObject element)
	{
		return (int)element.GetValue(ScrollResetTokenProperty);
	}

	public static void SetScrollResetToken(DependencyObject element, int value)
	{
		element.SetValue(ScrollResetTokenProperty, (object)value);
	}

	public static double GetContentTopOffset(DependencyObject element)
	{
		return (double)element.GetValue(ContentTopOffsetProperty);
	}

	public static void SetContentTopOffset(DependencyObject element, double value)
	{
		element.SetValue(ContentTopOffsetProperty, (object)value);
	}

	public static void Refresh(DependencyObject element)
	{
		if (element is ListBox listBox)
		{
			GetOptionalState(listBox)?.QueueRenderedItemStateRefresh();
		}
	}

	private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is ListBox listBox)
		{
			if ((bool)e.NewValue)
			{
				GetState(listBox).Attach(listBox);
			}
			else
			{
				GetOptionalState(listBox)?.Detach();
			}
		}
	}

	private static void OnEntranceAnimationTokenChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is ListBox listBox && GetIsEnabled((DependencyObject)(object)listBox))
		{
			int num = (int)e.NewValue;
			if (num > 0)
			{
				GetState(listBox).QueueEntranceAnimation(num);
			}
		}
	}

	private static void OnScrollResetTokenChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (dependencyObject is ListBox listBox)
		{
			int num = (int)e.NewValue;
			if (num > 0)
			{
				GetState(listBox).QueueScrollReset(num);
			}
		}
	}

	private static ListBoxState GetState(ListBox listBox)
	{
		if (((DependencyObject)listBox).GetValue(StateProperty) is ListBoxState result)
		{
			return result;
		}
		ListBoxState listBoxState = new ListBoxState();
		((DependencyObject)listBox).SetValue(StateProperty, (object)listBoxState);
		return listBoxState;
	}

	private static ListBoxState? GetOptionalState(ListBox listBox)
	{
		return ((DependencyObject)listBox).GetValue(StateProperty) as ListBoxState;
	}

	static VirtualizedListItemStateBehavior()
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Expected O, but got Unknown
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Expected O, but got Unknown
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Expected O, but got Unknown
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Expected O, but got Unknown
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Expected O, but got Unknown
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Expected O, but got Unknown
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Expected O, but got Unknown
		EntranceAnimationPassInterval = TimeSpan.FromMilliseconds(45.0);
		IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(VirtualizedListItemStateBehavior), new PropertyMetadata((object)false, new PropertyChangedCallback(OnIsEnabledChanged)));
		EntranceAnimationTokenProperty = DependencyProperty.RegisterAttached("EntranceAnimationToken", typeof(int), typeof(VirtualizedListItemStateBehavior), new PropertyMetadata((object)0, new PropertyChangedCallback(OnEntranceAnimationTokenChanged)));
		ScrollResetTokenProperty = DependencyProperty.RegisterAttached("ScrollResetToken", typeof(int), typeof(VirtualizedListItemStateBehavior), new PropertyMetadata((object)0, new PropertyChangedCallback(OnScrollResetTokenChanged)));
		ContentTopOffsetProperty = DependencyProperty.RegisterAttached("ContentTopOffset", typeof(double), typeof(VirtualizedListItemStateBehavior), new PropertyMetadata((object)0.0));
		StateProperty = DependencyProperty.RegisterAttached("State", typeof(ListBoxState), typeof(VirtualizedListItemStateBehavior), new PropertyMetadata((PropertyChangedCallback)null));
	}
}

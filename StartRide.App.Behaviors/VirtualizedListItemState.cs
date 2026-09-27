using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StartRide.App.Behaviors;

public sealed class VirtualizedListItemState : INotifyPropertyChanged
{
	private bool isFirstVisible;

	private bool isLastVisible;

	private bool isPreviousItemHighlighted;

	private bool shouldPlayEnterAnimation;

	private bool isEntranceAnimationPending;

	private int enterAnimationIndex;

	public bool IsFirstVisible
	{
		get
		{
			return isFirstVisible;
		}
		set
		{
			SetProperty(ref isFirstVisible, value, "IsFirstVisible");
		}
	}

	public bool IsLastVisible
	{
		get
		{
			return isLastVisible;
		}
		set
		{
			SetProperty(ref isLastVisible, value, "IsLastVisible");
		}
	}

	public bool IsPreviousItemHighlighted
	{
		get
		{
			return isPreviousItemHighlighted;
		}
		set
		{
			SetProperty(ref isPreviousItemHighlighted, value, "IsPreviousItemHighlighted");
		}
	}

	public bool ShouldPlayEnterAnimation
	{
		get
		{
			return shouldPlayEnterAnimation;
		}
		set
		{
			if (SetProperty(ref shouldPlayEnterAnimation, value, "ShouldPlayEnterAnimation") && !value)
			{
				IsEntranceAnimationPending = false;
			}
		}
	}

	public bool IsEntranceAnimationPending
	{
		get
		{
			return isEntranceAnimationPending;
		}
		set
		{
			SetProperty(ref isEntranceAnimationPending, value, "IsEntranceAnimationPending");
		}
	}

	public int EnterAnimationIndex
	{
		get
		{
			return enterAnimationIndex;
		}
		set
		{
			SetProperty(ref enterAnimationIndex, value, "EnterAnimationIndex");
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
		{
			return false;
		}
		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		return true;
	}
}

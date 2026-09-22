using System;

namespace Launcher.App.Services;

public sealed class FloatingMessageService : IFloatingMessageService
{
	private readonly object gate = new object();

	private string currentDragHint = string.Empty;

	private object? dragHintSource;

	public event Action<FloatingMessageRequest>? MessageRequested;

	public void Show(string message)
	{
		lock (gate)
		{
			currentDragHint = string.Empty;
			dragHintSource = null;
		}
		MessageRequested?.Invoke(new FloatingMessageRequest(message));
	}

	public void ShowDragHint(object source, string message)
	{
		ArgumentNullException.ThrowIfNull(source, "source");
		if (string.IsNullOrEmpty(message))
		{
			ClearDragHint(source);
			return;
		}
		lock (gate)
		{
			if (dragHintSource == source && string.Equals(currentDragHint, message, StringComparison.Ordinal))
			{
				return;
			}
			currentDragHint = message;
			dragHintSource = source;
		}
		MessageRequested?.Invoke(new FloatingMessageRequest(message, AutoHide: false));
	}

	public void ClearDragHint(object source)
	{
		ArgumentNullException.ThrowIfNull(source, "source");
		ClearDragHintCore(source);
	}

	public void ClearDragHint()
	{
		ClearDragHintCore(null);
	}

	private void ClearDragHintCore(object? owner)
	{
		lock (gate)
		{
			if (currentDragHint.Length == 0 || (owner != null && dragHintSource != owner))
			{
				return;
			}
			currentDragHint = string.Empty;
			dragHintSource = null;
		}
		MessageRequested?.Invoke(new FloatingMessageRequest(string.Empty));
	}
}

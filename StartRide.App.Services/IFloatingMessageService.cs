using System;

namespace StartRide.App.Services;

public interface IFloatingMessageService
{
	event Action<FloatingMessageRequest>? MessageRequested;

	void Show(string message);

	void ShowDragHint(object source, string message);

	void ClearDragHint(object source);

	void ClearDragHint();
}

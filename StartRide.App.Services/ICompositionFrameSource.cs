using System;

namespace StartRide.App.Services;

internal interface ICompositionFrameSource
{
	void Subscribe(EventHandler handler);

	void Unsubscribe(EventHandler handler);
}

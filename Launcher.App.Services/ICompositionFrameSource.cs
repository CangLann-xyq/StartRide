using System;

namespace Launcher.App.Services;

internal interface ICompositionFrameSource
{
	void Subscribe(EventHandler handler);

	void Unsubscribe(EventHandler handler);
}

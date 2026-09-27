using System;
using System.Windows.Media;

namespace StartRide.App.Services;

internal sealed class CompositionTargetFrameSource : ICompositionFrameSource
{
	internal static CompositionTargetFrameSource Instance { get; } = new CompositionTargetFrameSource();

	public void Subscribe(EventHandler handler)
	{
		CompositionTarget.Rendering += handler;
	}

	public void Unsubscribe(EventHandler handler)
	{
		CompositionTarget.Rendering -= handler;
	}
}

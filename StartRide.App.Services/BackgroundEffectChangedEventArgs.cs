using System;

namespace StartRide.App.Services;

public sealed class BackgroundEffectChangedEventArgs : EventArgs
{
	public string OldEffect { get; }

	public string NewEffect { get; }

	public BackgroundEffectChangedEventArgs(string oldEffect, string newEffect)
	{
		OldEffect = oldEffect;
		NewEffect = newEffect;
	}
}

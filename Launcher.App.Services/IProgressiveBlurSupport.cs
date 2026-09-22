using System;

namespace Launcher.App.Services;

internal interface IProgressiveBlurSupport : IDisposable
{
	ProgressiveBlurCapabilitySnapshot Current { get; }

	event EventHandler? AvailabilityChanged;
}

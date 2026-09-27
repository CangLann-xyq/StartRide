using System;

namespace StartRide.App.Services;

internal interface IProgressiveBlurSupport : IDisposable
{
	ProgressiveBlurCapabilitySnapshot Current { get; }

	event EventHandler? AvailabilityChanged;
}

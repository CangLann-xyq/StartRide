namespace StartRide.App.Services;

internal enum TransitionRenderCacheFallbackReason
{
	None,
	NoElements,
	RenderingTierTooLow,
	ElementNotReady,
	TextureTooLarge,
	MemoryBudgetExceeded,
	CacheCreationFailed
}

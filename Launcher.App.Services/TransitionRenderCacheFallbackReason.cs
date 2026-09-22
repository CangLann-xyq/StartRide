namespace Launcher.App.Services;

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

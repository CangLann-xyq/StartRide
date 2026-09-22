using System;

namespace Launcher.App.Controls;

[Flags]
internal enum BackdropBlurRefreshReason
{
	None = 0,
	Lifecycle = 1,
	Layout = 2,
	Scroll = 4,
	Size = 8,
	Source = 0x10,
	ContinuousAnimation = 0x20
}

namespace Launcher.App.Diagnostics;

internal readonly record struct VisualTreeSize(int ElementCount, int MaxDepth, bool IsTruncated, int EffectCount, int VisibleEffectCount, int DropShadowCount, int CardShadowCount, string EffectBreakdown, string VisibleEffectHosts);

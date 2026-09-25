namespace StartRide.App.Diagnostics;

internal static class UiRenderPaths
{
	internal const string BitmapCache = "BitmapCache";

	internal const string ContinuousBackdropRefresh = "ContinuousBackdropRefresh";

	internal const string Live = "Live";

	internal static string Resolve(bool usesBitmapCache, bool usesContinuousBackdropRefresh)
	{
		if (usesBitmapCache)
		{
			return "BitmapCache";
		}
		if (!usesContinuousBackdropRefresh)
		{
			return "Live";
		}
		return "ContinuousBackdropRefresh";
	}
}

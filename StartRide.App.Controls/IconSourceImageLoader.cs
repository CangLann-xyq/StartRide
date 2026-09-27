using System;
using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StartRide.App.Controls;

internal static class IconSourceImageLoader
{
	private sealed record CachedImageSource(ImageSource ImageSource, long? LastWriteTimeUtcTicks, long? FileLength);

	private const string ComponentPathPrefix = "/StartRide;component/";

	private const string ComponentUriPrefix = "pack://application:,,,/StartRide;component";

	private static readonly ConcurrentDictionary<string, CachedImageSource> CachedImagesByKey = new ConcurrentDictionary<string, CachedImageSource>(StringComparer.OrdinalIgnoreCase);

	public static ImageSource? TryLoad(object? source)
	{
		if (source is ImageSource result)
		{
			return result;
		}
		if (!(source is string text) || string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		try
		{
			Uri uri = CreateIconUri(text.Trim());
			if (TryGetCachedImage(uri, out ImageSource imageSource))
			{
				return imageSource;
			}
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			if (ShouldLoadImmediately(uri))
			{
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
			}
			bitmapImage.UriSource = uri;
			bitmapImage.EndInit();
			if (((Freezable)bitmapImage).CanFreeze)
			{
				((Freezable)bitmapImage).Freeze();
			}
			CacheImage(uri, bitmapImage);
			return bitmapImage;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static Uri CreateIconUri(string source)
	{
		if (Uri.TryCreate(source, UriKind.Absolute, out Uri result))
		{
			return result;
		}
		if (source.StartsWith("/StartRide;component/", StringComparison.OrdinalIgnoreCase))
		{
			return new Uri("pack://application:,,," + source, UriKind.Absolute);
		}
		if (source.StartsWith("/", StringComparison.Ordinal))
		{
			return new Uri("pack://application:,,,/StartRide;component" + source, UriKind.Absolute);
		}
		if (Uri.TryCreate(source, UriKind.Relative, out Uri result2))
		{
			return result2;
		}
		return new Uri(source, UriKind.RelativeOrAbsolute);
	}

	private static bool ShouldLoadImmediately(Uri uri)
	{
		if (!uri.IsFile)
		{
			return string.Equals(uri.Scheme, "pack", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static bool TryGetCachedImage(Uri uri, out ImageSource? imageSource)
	{
		imageSource = null;
		string text = TryCreateCacheKey(uri);
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		if (!CachedImagesByKey.TryGetValue(text, out CachedImageSource value))
		{
			return false;
		}
		if (uri.IsFile && !HasMatchingFileState(uri, value))
		{
			CachedImagesByKey.TryRemove(text, out CachedImageSource _);
			return false;
		}
		imageSource = value.ImageSource;
		return true;
	}

	private static void CacheImage(Uri uri, ImageSource imageSource)
	{
		string text = TryCreateCacheKey(uri);
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		if (uri.IsFile)
		{
			string localPath = uri.LocalPath;
			if (File.Exists(localPath))
			{
				FileInfo fileInfo = new FileInfo(localPath);
				CachedImagesByKey[text] = new CachedImageSource(imageSource, fileInfo.LastWriteTimeUtc.Ticks, fileInfo.Length);
			}
		}
		else
		{
			CachedImagesByKey[text] = new CachedImageSource(imageSource, null, null);
		}
	}

	private static string? TryCreateCacheKey(Uri uri)
	{
		if (uri.IsFile)
		{
			return uri.LocalPath;
		}
		if (string.Equals(uri.Scheme, "pack", StringComparison.OrdinalIgnoreCase))
		{
			return uri.AbsoluteUri;
		}
		return null;
	}

	private static bool HasMatchingFileState(Uri uri, CachedImageSource cached)
	{
		string localPath = uri.LocalPath;
		if (!File.Exists(localPath))
		{
			return false;
		}
		FileInfo fileInfo = new FileInfo(localPath);
		if (cached.LastWriteTimeUtcTicks == fileInfo.LastWriteTimeUtc.Ticks)
		{
			return cached.FileLength == fileInfo.Length;
		}
		return false;
	}
}

using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace StartRide.App.Services;

internal static class AuthlibInjectorServerDropParser
{
	internal const string Prefix = "authlib-injector:yggdrasil-server:";

	internal const int MaximumTextLength = 4096;

	private static readonly string[] TextFormats = new string[3]
	{
		DataFormats.UnicodeText,
		DataFormats.Text,
		DataFormats.StringFormat
	};

	public static AuthlibInjectorServerDropResult Parse(IDataObject dataObject)
	{
		string[] textFormats = TextFormats;
		foreach (string format in textFormats)
		{
			try
			{
				if (!dataObject.GetDataPresent(format, autoConvert: true) || !(dataObject.GetData(format, autoConvert: true) is string text))
				{
					continue;
				}
				return Parse(text);
			}
			catch (COMException)
			{
			}
			catch (InvalidOperationException)
			{
			}
		}
		return new AuthlibInjectorServerDropResult(AuthlibInjectorServerDropStatus.NotRecognized);
	}

	public static AuthlibInjectorServerDropResult Parse(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return new AuthlibInjectorServerDropResult(AuthlibInjectorServerDropStatus.NotRecognized);
		}
		string text2 = text.Trim();
		if (!text2.StartsWith("authlib-injector:yggdrasil-server:", StringComparison.OrdinalIgnoreCase))
		{
			return new AuthlibInjectorServerDropResult(AuthlibInjectorServerDropStatus.NotRecognized);
		}
		if (text2.Length > 4096)
		{
			return new AuthlibInjectorServerDropResult(AuthlibInjectorServerDropStatus.Invalid);
		}
		string text3 = text2.Substring("authlib-injector:yggdrasil-server:".Length);
		if (text3.Length == 0 || !HasValidPercentEncoding(text3))
		{
			return new AuthlibInjectorServerDropResult(AuthlibInjectorServerDropStatus.Invalid);
		}
		string text4;
		try
		{
			text4 = Uri.UnescapeDataString(text3).Trim();
		}
		catch (UriFormatException)
		{
			return new AuthlibInjectorServerDropResult(AuthlibInjectorServerDropStatus.Invalid);
		}
		if (text4.Length == 0)
		{
			return new AuthlibInjectorServerDropResult(AuthlibInjectorServerDropStatus.Invalid);
		}
		if (!text4.Contains("://", StringComparison.Ordinal))
		{
			text4 = "https://" + text4;
		}
		if (!Uri.TryCreate(text4, UriKind.Absolute, out Uri result) || !string.Equals(result.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(result.Host))
		{
			return new AuthlibInjectorServerDropResult(AuthlibInjectorServerDropStatus.Invalid);
		}
		return new AuthlibInjectorServerDropResult(AuthlibInjectorServerDropStatus.Valid, result.AbsoluteUri);
	}

	private static bool HasValidPercentEncoding(string value)
	{
		for (int i = 0; i < value.Length; i++)
		{
			if (value[i] == '%')
			{
				if (i + 2 >= value.Length || !Uri.IsHexDigit(value[i + 1]) || !Uri.IsHexDigit(value[i + 2]))
				{
					return false;
				}
				i += 2;
			}
		}
		return true;
	}
}

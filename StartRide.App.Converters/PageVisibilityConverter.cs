using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StartRide.App.Converters;

public sealed class PageVisibilityConverter : IValueConverter
{
	/// <summary>
	/// parameter 支持用 | 分隔多个页面名（如 "Install|Highlights"）——
	/// 同一个 View 被两个导航项共用时用得上。
	/// ⚠️ 不能用逗号：那是 XAML 标记扩展的参数分隔符（编译期 MC3042）。
	/// </summary>
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		string current = value?.ToString() ?? string.Empty;
		string wanted = parameter?.ToString() ?? string.Empty;
		if (wanted.Length == 0)
		{
			return Visibility.Collapsed;
		}
		foreach (string candidate in wanted.Split('|'))
		{
			if (string.Equals(current, candidate.Trim(), StringComparison.OrdinalIgnoreCase))
			{
				return Visibility.Visible;
			}
		}
		return Visibility.Collapsed;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}

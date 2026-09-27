using System;
using System.Globalization;
using System.Windows.Data;

namespace StartRide.App.Converters;

/// <summary>
/// 把绑定值减去一个量，并按 <c>ConverterParameter</c> 夹上下限。
///
/// 参数格式：<c>减数</c>、<c>减数|下限</c> 或 <c>减数|下限|上限</c>（竖线分隔，可留空；
/// 不能用逗号 —— XAML 标记扩展里逗号是参数分隔符，会报 MC3042）。
/// 例：
///   <c>ConverterParameter=96|420</c>         → max(值 − 96, 420)
///   <c>ConverterParameter=120|720|1080</c>   → clamp(值 − 120, 720, 1080)
///
/// 用途：浮层里的对话框要按宿主尺寸自适应 —— DialogHost 的 Surface 上下各有 24px
/// 外边距，正文阅读器还要再留自己的 22px 内边距，所以可用高度是宿主高减一个常量；
/// 窗口很小时又不能缩到没法读，于是还要一个下限。
/// </summary>
public sealed class SubtractConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		double input = value is double number && !double.IsNaN(number) ? number : 0d;
		double amount = 0d;
		double minimum = 0d;
		double maximum = double.PositiveInfinity;

		if (parameter is string text)
		{
			// ⚠️ XAML 标记扩展里逗号是参数分隔符，写 ConverterParameter=96,420 会直接报
		// MC3042；所以这里同时接受竖线分隔（XAML 用 |，代码里用 , 也行）。
		string[] parts = text.Split(',', '|');
			if (parts.Length > 0)
			{
				double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out amount);
			}
			if (parts.Length > 1)
			{
				double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out minimum);
			}
			if (parts.Length > 2)
			{
				double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out maximum);
			}
		}

		double result = input - amount;
		if (result < minimum)
		{
			result = minimum;
		}
		if (result > maximum)
		{
			result = maximum;
		}
		return result;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		=> throw new NotSupportedException("SubtractConverter 只用于单向绑定。");
}

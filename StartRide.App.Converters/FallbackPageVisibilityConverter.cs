using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using StartRide.App.Models;

namespace StartRide.App.Converters;

/// <summary>
/// 兜底页（GeneralPageView）的可见性：**只有 CurrentPage 没有专属宿主页面时才显示**。
///
/// ⚠️ 判据必须是「有没有专属页面」，不能在 XAML 里反过来枚举"已知页面名"。
/// 兜底页的 ScrollViewer 铺满整个内容区，一旦该收没收，它会：
///   ① 把 PageTitleConverter(CurrentPage) 与 StatusMessage 画在真正的页面上方（叠标题）；
///   ② 吃掉内容区**所有**鼠标点击 —— 导航栏在内容区外所以还能点，
///      于是表现成"页面能进、页内按钮全点不动"，极易误判成页面自身坏了。
/// 2026-09-26 新增「高光时刻(Highlights)」时就是这样踩的。
/// </summary>
public sealed class FallbackPageVisibilityConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return NavigationCatalog.HasDedicatedView(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}

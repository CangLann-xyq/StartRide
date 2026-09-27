using System.Windows.Controls;

namespace StartRide.App.Views.Shell;

/// <summary>
/// 条款阅读器的界面外壳。
///
/// 这个页面本身没有任何逻辑：数据在 <see cref="StartRide.App.ViewModels.Shell.LegalReaderViewModel"/>，
/// 正文块的渲染由 XAML 里的一堆 <c>DataTemplate</c> 完成。这里只保留 <c>InitializeComponent</c>，
/// 免得每次改 XAML 都要跟着动代码。
/// </summary>
public partial class LegalReaderPanel : UserControl
{
	public LegalReaderPanel()
	{
		InitializeComponent();
	}
}

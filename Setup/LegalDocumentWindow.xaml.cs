using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace StartRide.Setup;

/// <summary>
/// 安装向导里查看《用户协议》《隐私政策》的只读窗口。
/// </summary>
public partial class LegalDocumentWindow : Window
{
    public LegalDocumentWindow(string title, string resourceName)
    {
        InitializeComponent();

        HeaderText.Text = title;
        Title = "StartRide " + title;

        FlowDocument document = EmbeddedLegal.Build(resourceName, out bool found);
        Viewer.Document = document;

        if (!found)
        {
            HeaderText.Text = title + "（内容缺失）";
        }
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();
}

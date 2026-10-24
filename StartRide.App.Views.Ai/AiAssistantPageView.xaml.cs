using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using StartRide.App.ViewModels.Ai;

namespace StartRide.App.Views.Ai;

/// <summary>
/// AI 助手页面。
///
/// 代码后置只做两件 XAML 干不了的事：
///   1) 自动滚到底 —— 流式输出时正文每几十毫秒变一次，绑定做不到「跟着走」；
///      用户往上翻着看历史的时候不能硬拽回去，所以只在「正在生成」时才自动滚。
///   2) Enter 发送 / Shift+Enter 换行。
/// 其余全是绑定，VM 里不碰任何 UI 类型。
/// </summary>
public partial class AiAssistantPageView : UserControl
{
    private readonly DispatcherTimer scrollTimer;
    private INotifyCollectionChanged? hookedCollection;

    public AiAssistantPageView()
    {
        InitializeComponent();

        scrollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(260),
        };
        scrollTimer.Tick += OnScrollTimerTick;

        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => scrollTimer.Start();
        Unloaded += (_, _) => scrollTimer.Stop();
    }

    private AiAssistantViewModel? ViewModel => DataContext as AiAssistantViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (hookedCollection != null)
        {
            hookedCollection.CollectionChanged -= OnMessagesChanged;
            hookedCollection = null;
        }
        if (e.NewValue is AiAssistantViewModel vm)
        {
            hookedCollection = vm.Messages;
            hookedCollection.CollectionChanged += OnMessagesChanged;
        }
        ScrollToEndSoon();
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScrollToEndSoon();

    private void OnScrollTimerTick(object? sender, EventArgs e)
    {
        // 只在「模型正在输出」时跟随；空闲时把滚动位置留给用户
        if (ViewModel?.IsBusy == true && IsVisible)
        {
            ChatScroll.ScrollToEnd();
        }
    }

    private void ScrollToEndSoon()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try { ChatScroll.ScrollToEnd(); } catch { /* 还没布局完就算了 */ }
        }), DispatcherPriority.Background);
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        // Shift+Enter = 换行；单独 Enter = 发送
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) return;

        e.Handled = true;
        var vm = ViewModel;
        if (vm != null && vm.SendCommand.CanExecute(null))
        {
            vm.SendCommand.Execute(null);
        }
    }
}

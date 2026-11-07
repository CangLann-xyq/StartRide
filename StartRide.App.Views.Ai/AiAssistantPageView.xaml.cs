using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
using System.Text.RegularExpressions;
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

    /// <summary>左侧 .ai-turns 细条：把对应那一轮滚回视野。</summary>
    private void TurnMarker_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag != null)
        {
            (ChatItems.ItemContainerGenerator.ContainerFromItem(btn.Tag) as FrameworkElement)
                ?.BringIntoView();
        }
    }

    /// <summary>滚离底部才出现「回到最新」（参考程序 [data-latest]）。</summary>
    private void ChatScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeight <= 0) return;
        var away = ChatScroll.VerticalOffset + ChatScroll.ViewportHeight < ChatScroll.ExtentHeight - 48;
        LatestBtn.Visibility = away ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Latest_Click(object sender, RoutedEventArgs e) => ChatScroll.ScrollToEnd();

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

/// <summary>
/// AI 输出的轻量 Markdown 渲染（附加属性 Text -> TextBlock.Inlines）。
///
/// 参考程序 vendor/ai-markdown/format.js 会把标题 / 列表 / 引用 / 代码块 /
/// 行内代码 / 链接都渲染出来；WPF 的 TextBlock 只能吃纯文本，直接绑 Text 会把
/// ** 和 ` 原样露给用户。这里只覆盖模型日常回答里会出现的那几种，
/// 不引入第三方解析器，也不改 VM —— 纯显示层，给模型的历史文本仍是原文。
/// </summary>
public static class AiMarkdown
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached(
            "Text", typeof(string), typeof(AiMarkdown),
            new PropertyMetadata(null, OnTextChanged));

    public static string GetText(DependencyObject o) => (string)o.GetValue(TextProperty);
    public static void SetText(DependencyObject o, string value) => o.SetValue(TextProperty, value);

    private static readonly FontFamily Mono = new("Consolas, Microsoft YaHei UI");
    private static readonly SolidColorBrush CodeBrush = new(Color.FromRgb(0x9E, 0xC5, 0xF2));
    private static readonly SolidColorBrush MutedBrush = new(Color.FromRgb(0x92, 0xA8, 0xC4));
    private static readonly SolidColorBrush LinkBrush = new(Color.FromRgb(0x6F, 0xB0, 0xFF));

    private static readonly Regex InlineToken =
        new(@"(\*\*[^*]+\*\*|`[^`]+`|\[[^\]]+\]\([^)]+\))", RegexOptions.Compiled);
    private static readonly Regex Heading =
        new(@"^#{1,6}\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex ListItem =
        new(@"^\s*(?:[-*+]|\d+\.)\s+(.*)$", RegexOptions.Compiled);

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;
        tb.Inlines.Clear();
        var src = (e.NewValue as string ?? "").Replace("\r\n", "\n");
        if (src.Length == 0) return;

        var lines = src.Split('\n');
        var first = true;
        var inCode = false;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (!first) tb.Inlines.Add(new LineBreak());
            first = false;

            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inCode = !inCode;
                continue;
            }

            if (inCode)
            {
                tb.Inlines.Add(new Run(line)
                {
                    FontFamily = Mono,
                    FontSize = 12.5,
                    Foreground = CodeBrush,
                });
                continue;
            }

            if (line.Length == 0)
            {
                // 多补一个换行 = 段落间距（参考 .ai-message-text p { margin-bottom:12px }）
                tb.Inlines.Add(new LineBreak());
                continue;
            }

            var h = Heading.Match(line);
            if (h.Success)
            {
                tb.Inlines.Add(new Run(h.Groups[1].Value)
                {
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                });
                continue;
            }

            if (line.StartsWith("> ", StringComparison.Ordinal))
            {
                tb.Inlines.Add(new Run("│ ") { Foreground = LinkBrush });
                AppendInlines(tb.Inlines, line.Substring(2));
                continue;
            }

            var li = ListItem.Match(line);
            if (li.Success)
            {
                tb.Inlines.Add(new Run("   ·   ") { Foreground = MutedBrush });
                AppendInlines(tb.Inlines, li.Groups[1].Value);
                continue;
            }

            AppendInlines(tb.Inlines, line);
        }
    }

    private static void AppendInlines(InlineCollection col, string text)
    {
        var pos = 0;
        foreach (Match m in InlineToken.Matches(text))
        {
            if (m.Index > pos) col.Add(new Run(text.Substring(pos, m.Index - pos)));
            var tok = m.Value;

            if (tok.StartsWith("**", StringComparison.Ordinal))
            {
                col.Add(new Run(tok.Substring(2, tok.Length - 4))
                {
                    FontWeight = FontWeights.SemiBold,
                });
            }
            else if (tok.StartsWith("`", StringComparison.Ordinal))
            {
                col.Add(new Run(tok.Substring(1, tok.Length - 2))
                {
                    FontFamily = Mono,
                    FontSize = 12.5,
                    Foreground = CodeBrush,
                });
            }
            else
            {
                var link = Regex.Match(tok, @"^\[([^\]]+)\]\(([^)]+)\)$");
                if (link.Success && Uri.TryCreate(link.Groups[2].Value, UriKind.Absolute, out var uri))
                {
                    var hl = new Hyperlink(new Run(link.Groups[1].Value))
                    {
                        NavigateUri = uri,
                        Foreground = LinkBrush,
                        TextDecorations = TextDecorations.Underline,
                    };
                    hl.RequestNavigate += (_, ev) =>
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo(ev.Uri.AbsoluteUri)
                            {
                                UseShellExecute = true,
                            });
                        }
                        catch { /* 没有默认浏览器就算了，不影响回答 */ }
                        ev.Handled = true;
                    };
                    col.Add(hl);
                }
                else
                {
                    col.Add(new Run(tok));
                }
            }
            pos = m.Index + m.Length;
        }
        if (pos < text.Length) col.Add(new Run(text.Substring(pos)));
    }
}

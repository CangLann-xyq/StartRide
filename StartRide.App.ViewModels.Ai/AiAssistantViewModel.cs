using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using StartRide.Core;

namespace StartRide.App.ViewModels.Ai;

/// <summary>执行过程里的一步（一次工具调用）。</summary>
public sealed class AiStep : ObservableObject
{
    private string detail = "执行中…";
    private bool isError;
    private bool isRunning = true;

    public AiStep(string title, string args)
    {
        Title = title;
        Args = args;
    }

    public string Title { get; }

    /// <summary>模型传来的原始参数（折叠起来时也能看到它想干什么）。</summary>
    public string Args { get; }

    public string Detail
    {
        get => detail;
        set => SetProperty(ref detail, value);
    }

    public bool IsError
    {
        get => isError;
        set => SetProperty(ref isError, value);
    }

    public bool IsRunning
    {
        get => isRunning;
        set => SetProperty(ref isRunning, value);
    }

    public void Finish(bool ok, string output)
    {
        IsRunning = false;
        IsError = !ok;
        Detail = Summarize(output);
    }

    /// <summary>
    /// 面板里只放一小段：工具输出动辄几千行（列目录 / 命令回显），
    /// 原样塞进 UI 会把整屏铺满、把输入框顶出视野。
    /// 给模型的仍是完整内容（在 history 里），这里只影响「看起来」。
    /// </summary>
    private static string Summarize(string output)
    {
        if (string.IsNullOrEmpty(output)) return "（无输出）";
        const int maxLines = 6;
        const int maxChars = 700;

        var lines = output.Replace("\r\n", "\n").Split('\n');
        var body = lines.Length <= maxLines ? output : string.Join("\n", lines.Take(maxLines));
        var hidden = lines.Length - Math.Min(lines.Length, maxLines);
        if (body.Length > maxChars)
        {
            body = body.Substring(0, maxChars) + "…";
        }
        if (hidden > 0)
        {
            body += $"\n…（共 {lines.Length} 行，已折叠 {hidden} 行）";
        }
        return body;
    }
}

/// <summary>聊天区的一条消息。用户 / 助手 / 系统提示共用一个类型，靠 Role 决定模板。</summary>
public sealed class AiMessage : ObservableObject
{
    public const string RoleUser = "user";
    public const string RoleAssistant = "assistant";
    public const string RoleNotice = "notice";

    private string text = "";
    private string thinking = "";
    private bool isThinkingExpanded;
    private bool isStepsExpanded = true;
    private bool isStreaming;

    public AiMessage(string role) => Role = role;

    public string Role { get; }

    public bool IsUser => Role == RoleUser;
    public bool IsAssistant => Role == RoleAssistant;
    public bool IsNotice => Role == RoleNotice;

    public string Text
    {
        get => text;
        set
        {
            if (SetProperty(ref text, value))
            {
                OnPropertyChanged(nameof(HasText));
                OnPropertyChanged(nameof(ThinkingHeader));
            }
        }
    }

    public bool HasText => !string.IsNullOrEmpty(text);

    /// <summary>思考过程（deepseek-reasoner 的 reasoning_content）。</summary>
    public string Thinking
    {
        get => thinking;
        set
        {
            if (SetProperty(ref thinking, value))
            {
                OnPropertyChanged(nameof(HasThinking));
                OnPropertyChanged(nameof(ThinkingHeader));
            }
        }
    }

    public bool HasThinking => !string.IsNullOrEmpty(thinking);

    /// <summary>
    /// 折叠头文案。默认就是折叠的，但要让玩家知道「里面在动」——
    /// 所以在还没开始输出正文时写「思考中」，思考完改成字数。
    /// </summary>
    public string ThinkingHeader
        => (string.IsNullOrEmpty(text) ? "思考中 · 已想 " : "思考过程 · ")
           + thinking.Length + " 字";

    public bool IsThinkingExpanded
    {
        get => isThinkingExpanded;
        set => SetProperty(ref isThinkingExpanded, value);
    }

    public ObservableCollection<AiStep> Steps { get; } = new();

    public bool IsStepsExpanded
    {
        get => isStepsExpanded;
        set => SetProperty(ref isStepsExpanded, value);
    }

    public bool HasSteps => Steps.Count > 0;

    public string StepsHeader => "执行过程 · " + Steps.Count + " 步";

    public bool IsStreaming
    {
        get => isStreaming;
        set => SetProperty(ref isStreaming, value);
    }

    public void AddStep(AiStep step)
    {
        Steps.Add(step);
        OnPropertyChanged(nameof(HasSteps));
        OnPropertyChanged(nameof(StepsHeader));
    }

    public void RefreshStepsHeader() => OnPropertyChanged(nameof(StepsHeader));
}

/// <summary>
/// AI 助手页面的 ViewModel。
///
/// 两种模式：
///   对话（chat）  —— 只聊天，不带工具；可以用 deepseek-reasoner 看思考过程。
///   Agent        —— 带工具（列目录 / 读文件 / 写文件 / 执行命令），
///                   模型自己决定调哪几个；每一步都落在「执行过程」里可折叠查看。
///
/// 这件事的所有危险性都在权限开关上（AppSettings.AiAllow*），
/// VM 不做二次判断 —— 权限判断只在 AiToolHost 里做一次，避免两处口径不一致。
/// </summary>
public sealed class AiAssistantViewModel : ObservableObject
{
    private const int MaxRounds = 8;          // 一轮任务里最多来回几次（防止模型自己死循环）
    private const int MaxHistory = 26;        // 发给模型的历史条数上限
    private const int ToolResultSendLimit = 6000;

    private readonly AiChatClient client = new();
    private readonly AiToolHost tools = new();
    private readonly List<Dictionary<string, object?>> history = new();

    private CancellationTokenSource? cts;
    private string input = "";
    private bool isBusy;
    private bool isAgentMode;
    private string statusText = "就绪";
    private bool allowRead = true;
    private bool allowWrite;
    private bool allowExecute;
    private string workspaceRoot = "";
    private bool useReasoning;

    public AiAssistantViewModel()
    {
        var s = AppSettings.Current;
        allowRead = s.AiAllowRead;
        allowWrite = s.AiAllowWrite;
        allowExecute = s.AiAllowExecute;
        workspaceRoot = s.AiWorkspaceRoot;
        useReasoning = string.Equals(s.AiModel, AiChatClient.ReasoningModel, StringComparison.OrdinalIgnoreCase);
        isAgentMode = s.AiAgentMode;

        SendCommand = new RelayCommand(() => _ = SendAsync(), () => !IsBusy && !string.IsNullOrWhiteSpace(Input));
        StopCommand = new RelayCommand(Stop, () => IsBusy);
        ClearCommand = new RelayCommand(Clear, () => !IsBusy);
        PickFolderCommand = new RelayCommand(PickFolder);
        CheckHealthCommand = new RelayCommand(() => _ = CheckHealthAsync());

        Messages.Add(new AiMessage(AiMessage.RoleNotice)
        {
            Text = "我是 StartRide 内置的助手。对话模式可以直接问我问题；"
                   + "切到 Agent 模式后我还能读写文件、执行命令来帮你排查和修复游戏，"
                   + "但必须先在右边打开对应权限。",
        });
    }

    // ------------------------------------------------------------------
    // 状态
    // ------------------------------------------------------------------
    public ObservableCollection<AiMessage> Messages { get; } = new();

    public RelayCommand SendCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand PickFolderCommand { get; }
    public RelayCommand CheckHealthCommand { get; }

    public string Input
    {
        get => input;
        set
        {
            if (SetProperty(ref input, value))
            {
                SendCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(IsInputEmpty));
            }
        }
    }

    /// <summary>输入框空着时显示占位提示（WPF TextBox 没有原生 placeholder）。</summary>
    public bool IsInputEmpty => string.IsNullOrWhiteSpace(input);

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                SendCommand.NotifyCanExecuteChanged();
                StopCommand.NotifyCanExecuteChanged();
                ClearCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(CanEditPermissions));
            }
        }
    }

    public bool CanEditPermissions => !IsBusy;

    public string StatusText
    {
        get => statusText;
        private set => SetProperty(ref statusText, value);
    }

    public bool IsAgentMode
    {
        get => isAgentMode;
        set
        {
            if (SetProperty(ref isAgentMode, value))
            {
                AppSettings.Current.AiAgentMode = value;
                AppSettings.Current.Save();
                OnPropertyChanged(nameof(ModeHint));
            }
        }
    }

    public string ModeHint => IsAgentMode
        ? "Agent：可以调用工具（受右侧权限限制）"
        : "对话：只聊天，不动你的电脑";

    public bool UseReasoning
    {
        get => useReasoning;
        set
        {
            if (SetProperty(ref useReasoning, value))
            {
                AppSettings.Current.AiModel = value ? AiChatClient.ReasoningModel : AiChatClient.DefaultModel;
                AppSettings.Current.Save();
            }
        }
    }

    public bool AllowRead
    {
        get => allowRead;
        set
        {
            if (SetProperty(ref allowRead, value))
            {
                AppSettings.Current.AiAllowRead = value;
                AppSettings.Current.Save();
            }
        }
    }

    public bool AllowWrite
    {
        get => allowWrite;
        set
        {
            if (SetProperty(ref allowWrite, value))
            {
                AppSettings.Current.AiAllowWrite = value;
                AppSettings.Current.Save();
            }
        }
    }

    public bool AllowExecute
    {
        get => allowExecute;
        set
        {
            if (SetProperty(ref allowExecute, value))
            {
                AppSettings.Current.AiAllowExecute = value;
                AppSettings.Current.Save();
            }
        }
    }

    public string WorkspaceRoot
    {
        get => workspaceRoot;
        set
        {
            if (SetProperty(ref workspaceRoot, value))
            {
                AppSettings.Current.AiWorkspaceRoot = value;
                AppSettings.Current.Save();
            }
        }
    }

    public string EffectiveRoot => AiToolHost.WorkspaceRoot();

    // ------------------------------------------------------------------
    // 发送
    // ------------------------------------------------------------------
    private async Task SendAsync()
    {
        if (IsBusy) return;
        var text = (Input ?? "").Trim();
        if (text.Length == 0) return;

        Input = "";
        Messages.Add(new AiMessage(AiMessage.RoleUser) { Text = text });
        history.Add(new Dictionary<string, object?> { ["role"] = "user", ["content"] = text });
        TrimHistory();

        cts = new CancellationTokenSource();
        IsBusy = true;
        StatusText = "思考中…";

        try
        {
            var model = IsAgentMode
                ? AiChatClient.DefaultModel          // 工具调用只走 chat（reasoner 不支持 function calling）
                : (UseReasoning ? AiChatClient.ReasoningModel : AiChatClient.DefaultModel);
            var toolSchema = IsAgentMode ? AiToolHost.BuildToolSchema() : null;

            for (var round = 0; round < MaxRounds; round++)
            {
                var bubble = new AiMessage(AiMessage.RoleAssistant) { IsStreaming = true };
                Messages.Add(bubble);

                var started = DateTime.UtcNow;
                var res = await client.StreamAsync(
                    model,
                    BuildRequestMessages(),
                    toolSchema,
                    piece =>
                    {
                        bubble.Thinking += piece;
                        StatusText = "思考中…";
                    },
                    piece =>
                    {
                        // 思考结束、正文开始的那一帧：把思考块收起来。
                        // 要求就是「默认折叠、思考完自动折叠」——展开是用户手动点的，
                        // 收起来要自动，否则一段几百字的思考会把正文挤到屏幕外。
                        if (bubble.HasThinking && bubble.IsThinkingExpanded)
                            bubble.IsThinkingExpanded = false;
                        bubble.Text += piece;
                        StatusText = "生成中…";
                    },
                    cts.Token);

                bubble.IsStreaming = false;
                bubble.IsThinkingExpanded = false;

                if (!res.Ok)
                {
                    if (string.IsNullOrEmpty(bubble.Text) && string.IsNullOrEmpty(bubble.Thinking))
                        Messages.Remove(bubble);
                    Messages.Add(new AiMessage(AiMessage.RoleNotice) { Text = "⚠️ " + res.Error });
                    StatusText = "出错";
                    break;
                }

                // 有工具调用 → 逐个执行，然后带着结果再来一轮
                if (res.ToolCalls.Count > 0)
                {
                    history.Add(BuildAssistantToolCallMessage(res, bubble.Text));

                    foreach (var call in res.ToolCalls)
                    {
                        var step = new AiStep(AiToolHost.DisplayName(call.Name) + "：" + ShortArgs(call.Arguments),
                            call.Arguments);
                        bubble.AddStep(step);
                        bubble.RefreshStepsHeader();
                        StatusText = "执行 " + AiToolHost.DisplayName(call.Name) + "…";

                        var r = await tools.ExecuteAsync(call.Name, call.Arguments);
                        step.Finish(r.Ok, r.Output);

                        history.Add(new Dictionary<string, object?>
                        {
                            ["role"] = "tool",
                            ["tool_call_id"] = call.Id,
                            ["content"] = Clip(r.Output, ToolResultSendLimit),
                        });
                    }

                    TrimHistory();
                    continue;
                }

                history.Add(new Dictionary<string, object?>
                {
                    ["role"] = "assistant",
                    ["content"] = res.Content,
                });
                TrimHistory();

                var secs = (DateTime.UtcNow - started).TotalSeconds;
                StatusText = string.Format("完成 · {0:0.0}s", secs);
                break;
            }
        }
        catch (OperationCanceledException)
        {
            Messages.Add(new AiMessage(AiMessage.RoleNotice) { Text = "已停止。" });
            StatusText = "已停止";
        }
        catch (Exception ex)
        {
            Messages.Add(new AiMessage(AiMessage.RoleNotice) { Text = "⚠️ " + ex.Message });
            StatusText = "出错";
        }
        finally
        {
            cts?.Dispose();
            cts = null;
            IsBusy = false;
        }
    }

    private void Stop()
    {
        try { cts?.Cancel(); } catch { }
        StatusText = "正在停止…";
    }

    private void Clear()
    {
        if (IsBusy) return;
        Messages.Clear();
        history.Clear();
        Messages.Add(new AiMessage(AiMessage.RoleNotice) { Text = "已清空对话。" });
        StatusText = "就绪";
    }

    private void PickFolder()
    {
        var dlg = new OpenFolderDialog
        {
            Title = "选择 AI 可以访问的目录",
            Multiselect = false,
        };
        var cur = WorkspaceRoot;
        if (!string.IsNullOrWhiteSpace(cur) && System.IO.Directory.Exists(cur))
            dlg.InitialDirectory = cur;
        if (dlg.ShowDialog() == true)
        {
            WorkspaceRoot = dlg.FolderName;
            OnPropertyChanged(nameof(EffectiveRoot));
        }
    }

    private async Task CheckHealthAsync()
    {
        StatusText = "检查服务端…";
        using var c = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var r = await client.CheckHealthAsync(c.Token);
        Messages.Add(new AiMessage(AiMessage.RoleNotice) { Text = "服务端：" + r });
        StatusText = "就绪";
    }

    // ------------------------------------------------------------------
    // 上下文拼装
    // ------------------------------------------------------------------
    private string SystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 StartRide 启动器内置的 AI 助手（StartRide 是 BeamNG.drive 的第三方联机启动器，C#/WPF 实现）。");
        sb.AppendLine("回答用简体中文，简洁、直接、给可执行的下一步，不要客套话。");
        if (IsAgentMode)
        {
            sb.AppendLine("当前是 Agent 模式：你可以调用 list_dir / read_file / write_file / run_command。");
            sb.AppendLine("需要看文件就先 list_dir，再 read_file；改完要说明改了哪个文件、改了什么。");
            sb.AppendLine("所有路径都限制在工作目录内：" + AiToolHost.WorkspaceRoot());
            sb.AppendLine("如果工具返回「权限不足」，就告诉用户去右侧打开对应权限，不要换别的方式绕。");
            sb.AppendLine("执行命令时优先用只读命令（查版本、列目录）；不要执行会改系统设置或删数据的命令。");
        }
        else
        {
            sb.AppendLine("当前是对话模式：你不能读写文件或执行命令，只能给建议。");
        }
        return sb.ToString();
    }

    private List<Dictionary<string, object?>> BuildRequestMessages()
    {
        var list = new List<Dictionary<string, object?>>
        {
            new() { ["role"] = "system", ["content"] = SystemPrompt() },
        };
        list.AddRange(history);
        return list;
    }

    private static Dictionary<string, object?> BuildAssistantToolCallMessage(AiTurnResult res, string content)
    {
        var calls = new List<Dictionary<string, object?>>();
        foreach (var c in res.ToolCalls)
        {
            calls.Add(new Dictionary<string, object?>
            {
                ["id"] = string.IsNullOrEmpty(c.Id) ? "call_" + Guid.NewGuid().ToString("N")[..12] : c.Id,
                ["type"] = "function",
                ["function"] = new Dictionary<string, object?>
                {
                    ["name"] = c.Name,
                    ["arguments"] = string.IsNullOrWhiteSpace(c.Arguments) ? "{}" : c.Arguments,
                },
            });
        }
        return new Dictionary<string, object?>
        {
            ["role"] = "assistant",
            ["content"] = content ?? "",
            ["tool_calls"] = calls,
        };
    }

    private void TrimHistory()
    {
        while (history.Count > MaxHistory) history.RemoveAt(0);
        // 历史开头不能是 tool（没有对应的 assistant tool_calls 会被上游拒绝）
        while (history.Count > 0 && "tool".Equals(history[0].TryGetValue("role", out var r) ? r as string : null,
                   StringComparison.Ordinal))
        {
            history.RemoveAt(0);
        }
    }

    private static string Clip(string s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s ?? "" : s.Substring(0, max) + "\n…（已截断）";

    private static string ShortArgs(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) return "()";
        var s = args.Replace("\r", " ").Replace("\n", " ");
        return s.Length <= 60 ? s : s.Substring(0, 60) + "…";
    }
}

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
    private AiPermission policyRead = AiPermission.Allow;
    private AiPermission policyWrite = AiPermission.Ask;
    private AiPermission policyExec = AiPermission.Ask;
    private string workspaceRoot = "";
    private bool useReasoning;

    // ---- 确认流程 ------------------------------------------------------
    // 一轮任务里用户点过「本轮都允许」的工具（中文名，够用且不会和工具名漂移）
    private readonly HashSet<string> turnAllowedTools = new(StringComparer.Ordinal);
    private TaskCompletionSource<bool>? confirmTcs;
    private CancellationTokenRegistration confirmCancelReg;
    private string confirmAction = "";
    private string confirmTool = "";
    private string confirmTarget = "";
    private string confirmExtra = "";

    public AiAssistantViewModel()
    {
        var s = AppSettings.Current;
        policyRead = AiToolHost.ReadPolicy(s);
        policyWrite = AiToolHost.WritePolicy(s);
        policyExec = AiToolHost.ExecPolicy(s);
        workspaceRoot = s.AiWorkspaceRoot;
        useReasoning = string.Equals(s.AiModel, AiChatClient.ReasoningModel, StringComparison.OrdinalIgnoreCase);
        isAgentMode = s.AiAgentMode;

        SendCommand = new RelayCommand(() => _ = SendAsync(), () => !IsBusy && !string.IsNullOrWhiteSpace(Input));
        StopCommand = new RelayCommand(Stop, () => IsBusy);
        AllowOnceCommand = new RelayCommand(() => ResolveConfirm(true));
        DenyConfirmCommand = new RelayCommand(() => ResolveConfirm(false));
        AllowTurnCommand = new RelayCommand(AllowRestOfTurn,
            () => !string.IsNullOrEmpty(confirmTool));
        ClearCommand = new RelayCommand(Clear, () => !IsBusy);
        PickFolderCommand = new RelayCommand(PickFolder);
        CheckHealthCommand = new RelayCommand(() => _ = CheckHealthAsync());

        Messages.Add(new AiMessage(AiMessage.RoleNotice)
        {
            Text = "我是 StartRide 内置的助手。对话模式可以直接问我问题；"
                   + "切到 Agent 模式后我还能读写文件、执行命令来帮你排查和修复游戏。"
                   + "默认是「询问」——每一步动手之前我都会停下来问你要不要允许。",
        });
    }

    // ------------------------------------------------------------------
    // 状态
    // ------------------------------------------------------------------
    public ObservableCollection<AiMessage> Messages { get; } = new();

    public RelayCommand SendCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand AllowOnceCommand { get; }
    public RelayCommand DenyConfirmCommand { get; }
    public RelayCommand AllowTurnCommand { get; }
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
                OnPropertyChanged(nameof(PermissionsEnabled));
                OnPropertyChanged(nameof(AgentOffHintVisible));
            }
        }
    }

    public bool CanEditPermissions => !IsBusy;

    /// <summary>Agent 关着时在权限卡里显示一行提示（说明为什么灰着）。</summary>
    public bool AgentOffHintVisible => !IsAgentMode;

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
                // 权限区跟着 Agent 开关一起亮/灰
                OnPropertyChanged(nameof(PermissionsEnabled));
                OnPropertyChanged(nameof(AgentOffHintVisible));
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

    // ------------------------------------------------------------------
    // 三态权限
    //
    // 用「每个取值一个 bool」而不是把 enum 直接绑到 RadioButton：
    // 直接绑 enum 会需要转换器（比较式绑定），而 WPF 的 RadioButton 组又是
    // 「取消选中也会写 false」，转换器要区分「我被选上」和「别人被选上」，
    // 一不小心就把策略改错。三个 bool 各自写自己的值，最笨但最不会错。
    // ------------------------------------------------------------------
    private void SetPolicy(AiPermission value, bool isRead, bool isWrite)
    {
        if (isRead)
        {
            if (policyRead == value) return;
            policyRead = value;
            AppSettings.Current.AiPolicyRead = (int)value;
            OnPropertyChanged(nameof(ReadDeny));
            OnPropertyChanged(nameof(ReadAsk));
            OnPropertyChanged(nameof(ReadAllow));
            OnPropertyChanged(nameof(ReadPolicyHint));
        }
        else if (isWrite)
        {
            if (policyWrite == value) return;
            policyWrite = value;
            AppSettings.Current.AiPolicyWrite = (int)value;
            OnPropertyChanged(nameof(WriteDeny));
            OnPropertyChanged(nameof(WriteAsk));
            OnPropertyChanged(nameof(WriteAllow));
            OnPropertyChanged(nameof(WritePolicyHint));
        }
        else
        {
            if (policyExec == value) return;
            policyExec = value;
            AppSettings.Current.AiPolicyExecute = (int)value;
            OnPropertyChanged(nameof(ExecDeny));
            OnPropertyChanged(nameof(ExecAsk));
            OnPropertyChanged(nameof(ExecAllow));
            OnPropertyChanged(nameof(ExecPolicyHint));
        }
        AppSettings.Current.Save();
    }

    public bool ReadDeny { get => policyRead == AiPermission.Deny; set { if (value) SetPolicy(AiPermission.Deny, true, false); } }
    public bool ReadAsk { get => policyRead == AiPermission.Ask; set { if (value) SetPolicy(AiPermission.Ask, true, false); } }
    public bool ReadAllow { get => policyRead == AiPermission.Allow; set { if (value) SetPolicy(AiPermission.Allow, true, false); } }
    public string ReadPolicyHint => PolicyText(policyRead, "只读不会改动任何文件，一般可以直接允许");

    public bool WriteDeny { get => policyWrite == AiPermission.Deny; set { if (value) SetPolicy(AiPermission.Deny, false, true); } }
    public bool WriteAsk { get => policyWrite == AiPermission.Ask; set { if (value) SetPolicy(AiPermission.Ask, false, true); } }
    public bool WriteAllow { get => policyWrite == AiPermission.Allow; set { if (value) SetPolicy(AiPermission.Allow, false, true); } }
    public string WritePolicyHint => PolicyText(policyWrite, "会改你的文件，建议保持「询问」");

    public bool ExecDeny { get => policyExec == AiPermission.Deny; set { if (value) SetPolicy(AiPermission.Deny, false, false); } }
    public bool ExecAsk { get => policyExec == AiPermission.Ask; set { if (value) SetPolicy(AiPermission.Ask, false, false); } }
    public bool ExecAllow { get => policyExec == AiPermission.Allow; set { if (value) SetPolicy(AiPermission.Allow, false, false); } }
    public string ExecPolicyHint => PolicyText(policyExec, "等于把命令交出去，建议保持「询问」");

    private static string PolicyText(AiPermission p, string allowHint) => p switch
    {
        AiPermission.Deny => "禁止：模型会收到明确拒绝，不会再试",
        AiPermission.Ask => "每步都问你：看清要动哪个文件再点允许",
        _ => "直接允许：" + allowHint,
    };

    /// <summary>
    /// 权限区是否可操作。
    ///
    /// Agent 关掉时这三项本来就不生效 —— 灰掉比留着能点更诚实：
    /// 能点会让人以为「我开了写权限」，实际上对话模式根本没工具可用。
    /// </summary>
    public bool PermissionsEnabled => IsAgentMode && !IsBusy;

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
        var ct = cts.Token;
        IsBusy = true;
        StatusText = "思考中…";
        turnAllowedTools.Clear();

        // 本轮创建的所有气泡都要记下来。
        // 🔴 这里以前不记：中途任何异常都会跳过 `bubble.IsStreaming = false`，
        //    气泡就永远挂着「正在输出…」，而 IsBusy 已经在 finally 里变回 false
        //    （停止按钮跟着变灰）—— 表现就是「跑到一半没输出，还停不下来」。
        var bubbles = new List<AiMessage>();

        try
        {
            var model = IsAgentMode
                ? AiChatClient.DefaultModel          // 工具调用只走 chat（reasoner 不支持 function calling）
                : (UseReasoning ? AiChatClient.ReasoningModel : AiChatClient.DefaultModel);
            var toolSchema = IsAgentMode ? AiToolHost.BuildToolSchema() : null;

            var rounds = 0;
            for (; rounds < MaxRounds; rounds++)
            {
                ct.ThrowIfCancellationRequested();

                // 第二轮起，把上一轮气泡的「正在输出」收掉 —— 否则历史里会同时
                // 有好几个气泡在闪「正在输出…」，看起来像并行跑了好几件事。
                foreach (var old in bubbles) old.IsStreaming = false;

                var bubble = new AiMessage(AiMessage.RoleAssistant) { IsStreaming = true };
                bubbles.Add(bubble);
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
                        if (bubble.HasThinking && bubble.IsThinkingExpanded)
                            bubble.IsThinkingExpanded = false;
                        bubble.Text += piece;
                        StatusText = "生成中…";
                    },
                    ct);

                bubble.IsStreaming = false;
                bubble.IsThinkingExpanded = false;

                // ① 用户点了停止
                if (res.Cancelled)
                {
                    if (!bubble.HasText && !bubble.HasThinking && !bubble.HasSteps)
                        Messages.Remove(bubble);
                    StatusText = "已停止";
                    return;         // finally 会收拾干净
                }

                // ② 出错 / 断流
                if (!res.Ok)
                {
                    if (!bubble.HasText && !bubble.HasThinking && !bubble.HasSteps)
                        Messages.Remove(bubble);
                    else if (res.StreamBroken)
                    {
                        // 半截内容留着（对用户有用），只是明确标一句「这里断了」
                        bubble.Text += "\n\n〔输出在此中断〕";
                    }
                    Messages.Add(new AiMessage(AiMessage.RoleNotice) { Text = "⚠️ " + res.Error });
                    StatusText = res.StreamBroken ? "已中断" : "出错";
                    return;
                }

                // ③ 有工具调用 → 逐个执行，然后带着结果再来一轮
                if (res.ToolCalls.Count > 0)
                {
                    history.Add(BuildAssistantToolCallMessage(res, bubble.Text));

                    foreach (var call in res.ToolCalls)
                    {
                        ct.ThrowIfCancellationRequested();

                        var step = new AiStep(AiToolHost.DisplayName(call.Name) + "：" + ShortArgs(call.Arguments),
                            call.Arguments);
                        bubble.AddStep(step);
                        bubble.RefreshStepsHeader();
                        StatusText = "执行 " + AiToolHost.DisplayName(call.Name) + "…";

                        AiToolResult r;
                        try
                        {
                            r = await tools.ExecuteAsync(call.Name, call.Arguments, ct, ConfirmAsync);
                        }
                        catch (OperationCanceledException)
                        {
                            // 用户在这步中途点了停止。
                            // 不在这里收尾的话，这一步会永远停在「执行中…」——
                            // 界面上看起来像卡死了，其实整个任务已经停了。
                            step.Finish(false, "已停止：这一步被中断。");
                            throw;
                        }

                        step.Finish(r.Ok, r.Output);

                        history.Add(new Dictionary<string, object?>
                        {
                            ["role"] = "tool",
                            ["tool_call_id"] = call.Id,
                            ["content"] = Clip(r.Output, ToolResultSendLimit),
                        });
                    }

                    TrimHistory();
                    StatusText = "整理结果…";
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
                return;
            }

            // 走满 MaxRounds 还没收尾：必须说清楚，否则用户看到的就是「说到一半停了」
            Messages.Add(new AiMessage(AiMessage.RoleNotice)
            {
                Text = $"⚠️ 已经连续调用 {MaxRounds} 轮工具，为防死循环先停在这里。"
                       + "可以把任务拆小一点再发，或者告诉我继续。",
            });
            StatusText = "已达轮次上限";
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
            // 无论怎么退出，所有气泡都必须收掉「正在输出」—— 这是那个卡住 bug 的根治点
            foreach (var b in bubbles) b.IsStreaming = false;

            ClearPendingConfirm(false);
            turnAllowedTools.Clear();
            cts?.Dispose();
            cts = null;
            IsBusy = false;
        }
    }

    // ------------------------------------------------------------------
    // 三态权限里的「每次问我」
    // ------------------------------------------------------------------
    /// <summary>
    /// 工具层在策略=询问时调它。
    ///
    /// 注意这是**在 UI 线程上 await 的**（SendAsync 一路没有 ConfigureAwait(false)），
    /// 所以挂起期间消息泵还在跑，用户点按钮有反应。
    /// 但绝不能弹模态对话框：那会把 SendAsync 的继续执行卡在别的消息循环里，
    /// 一旦用户关窗就再也没人 set 结果。用页面内的确认条最稳。
    /// </summary>
    private Task<bool> ConfirmAsync(AiConfirmRequest req)
    {
        // 本轮已经点过「都允许」
        if (turnAllowedTools.Contains(req.Tool)) return Task.FromResult(true);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        confirmTcs = tcs;
        confirmAction = req.Action;
        confirmTool = req.Tool;
        confirmTarget = req.Target;
        confirmExtra = req.Extra;

        OnPropertyChanged(nameof(HasPendingConfirm));
        OnPropertyChanged(nameof(ConfirmTitle));
        OnPropertyChanged(nameof(ConfirmTarget));
        OnPropertyChanged(nameof(ConfirmExtra));
        OnPropertyChanged(nameof(HasConfirmExtra));
        AllowTurnCommand.NotifyCanExecuteChanged();
        StatusText = "等你确认…";

        // 用户这会儿点「停止」也要能把确认条收掉，否则界面会留个孤儿提示条
        confirmCancelReg = (cts?.Token ?? CancellationToken.None).Register(() => ResolveConfirm(false));
        return tcs.Task;
    }

    private void ResolveConfirm(bool allowed)
    {
        var tcs = confirmTcs;
        if (tcs == null) return;
        ClearPendingConfirm(allowed);
        tcs.TrySetResult(allowed);
    }

    private void AllowRestOfTurn()
    {
        var tool = confirmTool;
        if (!string.IsNullOrEmpty(tool)) turnAllowedTools.Add(tool);
        ResolveConfirm(true);
    }

    private void ClearPendingConfirm(bool allowed)
    {
        confirmTcs = null;
        try { confirmCancelReg.Dispose(); } catch { }
        confirmCancelReg = default;
        confirmAction = "";
        confirmTool = "";
        confirmTarget = "";
        confirmExtra = "";
        OnPropertyChanged(nameof(HasPendingConfirm));
        OnPropertyChanged(nameof(ConfirmTitle));
        OnPropertyChanged(nameof(ConfirmTarget));
        OnPropertyChanged(nameof(ConfirmExtra));
        OnPropertyChanged(nameof(HasConfirmExtra));
        AllowTurnCommand.NotifyCanExecuteChanged();
    }

    public bool HasPendingConfirm => confirmTcs != null;

    public string ConfirmTitle => string.IsNullOrEmpty(confirmAction)
        ? ""
        : "要" + confirmAction + "，允许吗？";

    public string ConfirmTarget => confirmTarget;

    public string ConfirmExtra => confirmExtra;

    public bool HasConfirmExtra => !string.IsNullOrEmpty(confirmExtra);

    private void Stop()
    {
        // 先收确认条：不然用户点停止后那个提示条还挂着，点「允许」也没人接
        ResolveConfirm(false);
        try { cts?.Cancel(); } catch { }
        // 立刻变文案，别让用户以为没点上 —— 真正收尾在 finally，
        // 但那要等正在跑的命令被杀掉（token 一 cancel，RunCommandAsync 会杀进程树）
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
            sb.AppendLine("如果工具返回「权限不足」，就告诉用户去右侧把那一项改成「询问」或「允许」，不要换别的方式绕。");
            sb.AppendLine("如果工具返回「用户拒绝」，就停下这一步，不要重复调用同一个工具。");
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

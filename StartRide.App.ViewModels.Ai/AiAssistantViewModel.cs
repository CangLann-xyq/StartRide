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

/// <summary>执行步骤横排里一个编号位的状态。</summary>
public enum AiStepState
{
    Pending,
    Running,
    Complete,
    Error,
}

/// <summary>
/// 「执行步骤」横排里的一个编号位。
///
/// 1:1 复刻参考站 auth-panel_steps 的语义：23px 圆形编号徽标 + 一小段标题，
/// 三态（待执行 / 进行中 / 已完成），这里多一个失败态。
/// 一轮任务固定 8 个位置，先占位的先亮；跑满 8 步必须停 —— 这既是 UI，
/// 也是 SendAsync 的硬闸门，界面上看到的数字和真实执行次数永远是同一个数。
/// </summary>
public sealed class AiStepSlot : ObservableObject
{
    private string title = "";
    private string detail = "等待执行";
    private AiStepState state = AiStepState.Pending;

    public AiStepSlot(int index) => Index = index;

    public int Index { get; }

    public string Badge => (Index + 1).ToString();

    public string Title
    {
        get => title;
        set => SetProperty(ref title, value);
    }

    public string Detail
    {
        get => detail;
        set => SetProperty(ref detail, value);
    }

    public AiStepState State
    {
        get => state;
        set
        {
            if (!SetProperty(ref state, value)) return;
            OnPropertyChanged(nameof(IsPending));
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsComplete));
            OnPropertyChanged(nameof(IsError));
        }
    }

    public bool IsPending => state == AiStepState.Pending;
    public bool IsRunning => state == AiStepState.Running;
    public bool IsComplete => state == AiStepState.Complete;
    public bool IsError => state == AiStepState.Error;

    public void Start(string stepTitle)
    {
        Title = stepTitle;
        Detail = "执行中…";
        State = AiStepState.Running;
    }

    public void Finish(bool ok, string output)
    {
        Detail = string.IsNullOrWhiteSpace(output) ? "（无输出）" : output;
        State = ok ? AiStepState.Complete : AiStepState.Error;
    }

    public void Reset()
    {
        Title = "";
        Detail = "等待执行";
        State = AiStepState.Pending;
    }
}

/// <summary>执行过程里的一步（一次工具调用）。</summary>
public sealed class AiStep : ObservableObject
{
    private string detail = "执行中…";
    private bool isError;
    private bool isRunning = true;

    public AiStep(string title, string args, int textOffset = 0)
    {
        Title = title;
        Args = args;
        TextOffset = textOffset;
    }

    public string Title { get; }

    /// <summary>这一步出现时正文已经写到了哪个字符（执行过程要插在正文中间）。</summary>
    public int TextOffset { get; }

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
    private string plan = "";
    private bool isPlanExpanded;
    private bool isUnderstanding;
    private bool understandingDone;
    private bool isThinkingExpanded;
    // 执行过程默认折叠：步骤展开会把正文顶出视野，要看的人自己点开。
    private bool isStepsExpanded;
    private bool isStreaming;
    private string durationText = "正在回答…";
    private bool stepsAutoOpened;
    private readonly DateTime startedAt = DateTime.Now;

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
                RefreshBodySplit();
            }
        }
    }

    public bool HasText => !string.IsNullOrEmpty(text);

    /// <summary>
    /// 正文按「第一次工具调用发生时的长度」切成两段，折叠块插在中间。
    /// 1:1 参考程序 AiChatDetails.renderSteps：先写出的那段文字留在原位，
    /// 后续输出接在折叠块下方，工具记录不会把整段答案顶下去。
    /// </summary>
    private int SplitAt => Steps.Count == 0
        ? 0
        : Math.Min(text.Length, Math.Max(0, Steps[0].TextOffset));

    public string TextBefore => Steps.Count == 0 ? "" : text.Substring(0, SplitAt);
    public string TextAfter => Steps.Count == 0 ? text : text.Substring(SplitAt);
    public string BodyText => Steps.Count == 0 ? text : TextAfter;
    public bool HasTextBefore => Steps.Count > 0 && SplitAt > 0;

    private void RefreshBodySplit()
    {
        OnPropertyChanged(nameof(TextBefore));
        OnPropertyChanged(nameof(TextAfter));
        OnPropertyChanged(nameof(BodyText));
        OnPropertyChanged(nameof(HasTextBefore));
    }

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
    /// 「理解与计划」：Agent 多轮里，模型在调工具之前写的那段说明文字。
    /// 它是过程不是答案 —— 折到这里（默认收起），别让它以正文姿态刷屏。
    /// </summary>
    public string Plan
    {
        get => plan;
        set
        {
            if (SetProperty(ref plan, value))
            {
                OnPropertyChanged(nameof(HasPlan));
                OnPropertyChanged(nameof(PlanHeader));
            }
        }
    }

    public bool HasPlan => !string.IsNullOrEmpty(plan);

    public string PlanHeader => "理解与计划 · " + plan.Length + " 字";

    public bool IsPlanExpanded
    {
        get => isPlanExpanded;
        set => SetProperty(ref isPlanExpanded, value);
    }

    /// <summary>
    /// 「正在理解需求」占位：消息发出去到首轮响应之间的一条状态行。
    /// 模型一开始产出（思考/正文/工具调用）就置 UnderstandingDone，整行自动消失 ——
    /// 用属性而不是把消息从集合里删掉：属性变更 WPF 会跨线程封送，
    /// 在流式回调线程上动 ObservableCollection 会直接炸。
    /// </summary>
    public bool IsUnderstanding
    {
        get => isUnderstanding;
        set => SetProperty(ref isUnderstanding, value);
    }

    public bool UnderstandingDone
    {
        get => understandingDone;
        set
        {
            if (SetProperty(ref understandingDone, value))
                OnPropertyChanged(nameof(IsUnderstandingVisible));
        }
    }

    public bool IsUnderstandingVisible => isUnderstanding && !understandingDone;

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

    /// <summary>
    /// 折叠头文案，1:1 参考程序 AiChatDetails.renderSteps 的汇总口径：
    /// 有失败 / 需确认 → 需要你查看；有进行中 → 正在检查；否则 → 已完成 N 项检查。
    /// </summary>
    public string StepsSummary
    {
        get
        {
            if (StepsNeedAttention) return "诊断操作 · 需要你查看";
            if (Steps.Any(x => x.IsRunning)) return "正在检查 · " + Steps.Count + " 项";
            return "已完成 " + Steps.Count + " 项检查";
        }
    }

    public bool StepsNeedAttention => Steps.Any(x => x.IsError);
    public string StepsHeader => StepsSummary;

    /// <summary>消息顶部的耗时行（参考程序 .ai-duration）。</summary>
    public string DurationText
    {
        get => durationText;
        private set => SetProperty(ref durationText, value);
    }

    public string TimeText => startedAt.ToString("HH:mm");

    /// <summary>本轮收尾时把耗时定格，别让流式刷新一直改这个数。</summary>
    public void MarkDone() => DurationText =
        "耗时 " + (DateTime.Now - startedAt).TotalSeconds.ToString("0.0") + " s";

    public bool IsStreaming
    {
        get => isStreaming;
        set => SetProperty(ref isStreaming, value);
    }

    public void AddStep(AiStep step)
    {
        Steps.Add(step);
        RefreshStepsHeader();
        RefreshBodySplit();
    }

    /// <summary>步骤状态变化后重算折叠头与正文切分（IsRunning / IsError 会变）。</summary>
    public void RefreshStepsHeader()
    {
        OnPropertyChanged(nameof(HasSteps));
        OnPropertyChanged(nameof(StepsHeader));
        OnPropertyChanged(nameof(StepsSummary));
        OnPropertyChanged(nameof(StepsNeedAttention));
        RefreshBodySplit();
        // 参考程序 box.open = urgent || manualOpen：只有「需要你查看」才自动展开，
        // 正在执行时只把汇总文案改成「正在检查」，不抢用户的屏幕。
        if (!stepsAutoOpened && StepsNeedAttention)
        {
            stepsAutoOpened = true;
            IsStepsExpanded = true;
        }
    }
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
    // ---- 一轮任务的硬闸门（「能跑满 8 步」和「绝不死循环」同时成立） ----------
    // StepCapacity 既是一轮任务允许的真实工具执行次数，也是「已完成 N 项检查」的上限。
    // UI 侧改成参考程序那种按消息折叠的 details 列表，不再铺固定 8 位的横排刻度。
    // 以前只有 MaxRounds=8 一个「模型调用次数」上限：模型只要中途回一次不带工具的
    // 空内容、或上游抖一下返回 5xx，整轮就 return 了 —— 玩家看到的就是「没执行 8 步
    // 就停下」。现在把「步骤数 / 模型调用数 / 墙钟时间 / 重复调用 / 空回复」拆成
    // 五个互不依赖的硬闸门，任何一个到顶都必须停，但没到顶就不许提前收工。
    public const int StepCapacity = 8;
    private const int MaxModelCalls = StepCapacity + 2;   // 8 步 + 收尾成文 + 1 次容错
    private const int MaxIdenticalCalls = 3;              // 同工具+同参连续 3 次 = 模型在原地打转
    private const int MaxEmptyReplies = 2;                // 连续空正文最多重试 2 次
    private const int MaxTransientErrors = 2;             // 上游 5xx/断流最多自动重试 2 次
    private static readonly TimeSpan TurnTimeout = TimeSpan.FromMinutes(8);

    private const int MaxHistory = 26;        // 发给模型的历史条数上限
    private const int ToolResultSendLimit = 6000;

    private readonly AiChatClient client = new();
    private readonly AiToolHost tools = new();
    private readonly List<Dictionary<string, object?>> history = new();

    private CancellationTokenSource? cts;
    // 本轮的「用户停止 + 全局超时」合并令牌。确认条必须挂在它上面，
    // 只挂在 cts 上的话，8 分钟超时触发时用户正停在「允许吗」这条上，
    // 没有任何人去 set 那个 TaskCompletionSource → 整轮永远挂着。
    private CancellationToken turnToken;
    private string input = "";
    private bool isBusy;
    private bool isAgentMode;
    private string statusText = "就绪";
    private AiPermission policyRead = AiPermission.Allow;
    private AiPermission policyWrite = AiPermission.Ask;
    private AiPermission policyExec = AiPermission.Ask;
    private string workspaceRoot = "";
    private bool useReasoning;
    private AiQuota quota = new() { Error = "正在读取额度…" };

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

        for (var i = 0; i < StepCapacity; i++) StepSlots.Add(new AiStepSlot(i));
        stepRailVisible = isAgentMode;

        SendCommand = new RelayCommand(() => _ = SendAsync(), () => !IsBusy && !string.IsNullOrWhiteSpace(Input));
        StopCommand = new RelayCommand(Stop, () => IsBusy);
        AllowOnceCommand = new RelayCommand(() => ResolveConfirm(true));
        DenyConfirmCommand = new RelayCommand(() => ResolveConfirm(false));
        AllowTurnCommand = new RelayCommand(AllowRestOfTurn,
            () => !string.IsNullOrEmpty(confirmTool));
        ClearCommand = new RelayCommand(Clear, () => !IsBusy);
        PickFolderCommand = new RelayCommand(PickFolder);
        CheckHealthCommand = new RelayCommand(() => _ = CheckHealthAsync());
        _ = RefreshQuotaAsync();      // 进页面就读一次本周额度，别等第一次对话完才显示

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

    // ------------------------------------------------------------------
    // 执行步骤横排（一轮任务 8 个编号位）
    //
    // 固定 8 个槽位而不是「跑一步加一个」：玩家一进 Agent 模式就能看到完整
    // 的 8 步刻度，跑到第几步一目了然，也不会出现「还没到 8 步就停了」的错觉
    // —— 剩下几位会明确留在「待执行」。
    // ------------------------------------------------------------------
    public ObservableCollection<AiStepSlot> StepSlots { get; } = new();

    private int executedSteps;
    private AiStepSlot? currentSlot;
    private bool stepRailVisible;

    public bool StepRailVisible
    {
        get => stepRailVisible;
        private set => SetProperty(ref stepRailVisible, value);
    }

    public string StepRailHeader => "执行步骤 · " + executedSteps + " / " + StepCapacity;

    public bool HasCurrentStep => currentSlot != null;

    public string CurrentStepTitle => currentSlot == null
        ? ""
        : "第 " + (currentSlot.Index + 1) + " 步 · " + currentSlot.Title;

    public string CurrentStepDetail => currentSlot?.Detail ?? "";

    public bool CurrentStepIsError => currentSlot?.IsError ?? false;

    /// <summary>每轮开始前把 8 个刻度擦干净（不重建集合，避免 WPF 绑定断掉）。</summary>
    private void ResetStepRail()
    {
        executedSteps = 0;
        currentSlot = null;
        foreach (var slot in StepSlots) slot.Reset();
        RefreshStepRail();
    }

    private void RefreshStepRail()
    {
        OnPropertyChanged(nameof(StepRailHeader));
        OnPropertyChanged(nameof(HasCurrentStep));
        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(CurrentStepDetail));
        OnPropertyChanged(nameof(CurrentStepIsError));
    }

    private void SelectSlot(AiStepSlot slot)
    {
        currentSlot = slot;
        RefreshStepRail();
    }

    /// <summary>
    /// 收尾：任何还在「执行中」的槽位都必须落地。
    /// 少这一步的话，中断/超时/异常之后横排会永远亮着一个转圈的蓝点。
    /// </summary>
    private void AbortRunningSlots(string reason)
    {
        foreach (var slot in StepSlots)
        {
            if (slot.IsRunning) slot.Finish(false, reason);
        }
        RefreshStepRail();
    }

    public RelayCommand SendCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand AllowOnceCommand { get; }
    public RelayCommand DenyConfirmCommand { get; }
    public RelayCommand AllowTurnCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand PickFolderCommand { get; }
    public RelayCommand CheckHealthCommand { get; }

    // ------------------------------------------------------------------
    // 本周额度（服务端按账号计费，客户端只做显示与提前劝阻）
    // ------------------------------------------------------------------
    public AiQuota Quota
    {
        get => quota;
        private set
        {
            quota = value;
            OnPropertyChanged(nameof(Quota));
            OnPropertyChanged(nameof(QuotaSummary));
            OnPropertyChanged(nameof(QuotaDetail));
            OnPropertyChanged(nameof(QuotaPercent));
            OnPropertyChanged(nameof(QuotaWarning));
            OnPropertyChanged(nameof(QuotaExhausted));
        }
    }

    public string QuotaSummary => quota.Summary;
    public string QuotaDetail => quota.Detail;
    public double QuotaPercent => quota.Percent;

    /// <summary>用了 80% 以上就变色提醒，别等到弹「额度用完」才让人知道。</summary>
    public bool QuotaWarning => quota.Error == null && (quota.Percent >= 80 || quota.Exhausted);
    public bool QuotaExhausted => quota.Error == null && quota.Exhausted;

    public async Task RefreshQuotaAsync()
    {
        try
        {
            Quota = await AiChatClient.GetQuotaAsync(CancellationToken.None);
        }
        catch
        {
            // 查额度失败只是少一行信息，不该打断对话
        }
    }

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
                StepRailVisible = value;
                if (!value) ResetStepRail();
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
        if (IsAgentMode)
        {
            StepRailVisible = true;
            ResetStepRail();
        }

        // 全局硬超时：和用户「停止」共用一条链路，但两者要能分辨
        //（超时是安全收尾，不是用户主动取消）。用 linked CTS 而不是去 Cancel
        // 用户那一侧的 cts，Stop() 之后才判断得出到底是谁触发的。
        using var turnCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        turnCts.CancelAfter(TurnTimeout);
        var turnCt = turnCts.Token;
        turnToken = turnCt;

        // 「正在理解需求」占位：首轮响应一到就自动消失（见 understanding 标记）。
        var understanding = new AiMessage(AiMessage.RoleAssistant) { IsUnderstanding = true };
        Messages.Add(understanding);

        // 本轮创建的所有气泡都要记下来。
        // 🔴 这里以前不记：中途任何异常都会跳过 `bubble.IsStreaming = false`，
        //    气泡就永远挂着「正在输出…」，而 IsBusy 已经在 finally 里变回 false
        //    （停止按钮跟着变灰）—— 表现就是「跑到一半没输出，还停不下来」。
        var bubbles = new List<AiMessage>();

        var executed = 0;          // 真实执行过的工具步数（= 横排上亮起的格子数）
        var modelCalls = 0;
        var emptyReplies = 0;
        var transientErrors = 0;
        var identicalCalls = 0;
        var lastCallKey = "";
        string? stopReason = null;

        try
        {
            var model = IsAgentMode
                ? AiChatClient.DefaultModel          // 工具调用只走 chat（reasoner 不支持 function calling）
                : (UseReasoning ? AiChatClient.ReasoningModel : AiChatClient.DefaultModel);
            var toolSchema = IsAgentMode ? AiToolHost.BuildToolSchema() : null;

            while (true)
            {
                // 五个硬闸门：任何一个到顶都必须停，且都要有明确文案。
                // ThrowIfCancellationRequested 放最前面，保证全局超时 / 用户停止
                // 一定落到 finally，绝不会出现「跑到一半没人收尾」。
                turnCt.ThrowIfCancellationRequested();

                if (modelCalls >= MaxModelCalls)
                {
                    stopReason = "模型调用次数已达 " + MaxModelCalls + " 次上限";
                    break;
                }
                if (executed >= StepCapacity)
                {
                    stopReason = StepCapacity + " 个执行步骤已全部跑完";
                    break;
                }

                // 第二轮起，把上一轮气泡的「正在输出」收掉 —— 否则历史里会同时
                // 有好几个气泡在闪「正在输出…」，看起来像并行跑了好几件事。
                foreach (var old in bubbles) old.IsStreaming = false;

                var bubble = new AiMessage(AiMessage.RoleAssistant) { IsStreaming = true };
                bubbles.Add(bubble);
                Messages.Add(bubble);

                var started = DateTime.UtcNow;
                modelCalls++;
                var res = await client.StreamAsync(
                    model,
                    BuildRequestMessages(),
                    toolSchema,
                    piece =>
                    {
                        understanding.UnderstandingDone = true;
                        bubble.Thinking += piece;
                        StatusText = "思考中…";
                    },
                    piece =>
                    {
                        understanding.UnderstandingDone = true;
                        // 思考结束、正文开始的那一帧：把思考块收起来。
                        if (bubble.HasThinking && bubble.IsThinkingExpanded)
                            bubble.IsThinkingExpanded = false;
                        bubble.Text += piece;
                        StatusText = "生成中…";
                    },
                    turnCt);

                bubble.IsStreaming = false;
                bubble.IsThinkingExpanded = false;
                understanding.UnderstandingDone = true;

                // ① 用户点了停止 / 全局超时
                if (res.Cancelled || turnCt.IsCancellationRequested)
                {
                    if (!bubble.HasText && !bubble.HasThinking && !bubble.HasSteps)
                        Messages.Remove(bubble);
                    if (ct.IsCancellationRequested)
                    {
                        StatusText = "已停止";
                    }
                    else
                    {
                        Messages.Add(new AiMessage(AiMessage.RoleNotice)
                        {
                            Text = "本轮达到 " + (int)TurnTimeout.TotalMinutes
                                   + " 分钟硬超时，已自动停止（不会继续空转）。可以把任务拆小再发。",
                        });
                        StatusText = "已超时停止";
                    }
                    return;         // finally 会收拾干净
                }

                // ② 上游 5xx / 断流：这是「没跑满 8 步就停」的头号原因。
                //    以前一次失败就 return，现在原地重试，重试不占步骤额度。
                if (!res.Ok)
                {
                    if (!bubble.HasText && !bubble.HasThinking && !bubble.HasSteps)
                        Messages.Remove(bubble);
                    transientErrors++;
                    if (transientErrors <= MaxTransientErrors)
                    {
                        Messages.Add(new AiMessage(AiMessage.RoleNotice)
                        {
                            Text = "上游暂时不可用（" + res.Error + "），自动重试 "
                                   + transientErrors + "/" + MaxTransientErrors + "…",
                        });
                        StatusText = "重试中…";
                        await Task.Delay(700, turnCt);
                        continue;
                    }
                    Messages.Add(new AiMessage(AiMessage.RoleNotice) { Text = res.Error });
                    StatusText = res.StreamBroken ? "已中断" : "出错";
                    return;
                }
                transientErrors = 0;

                // ③ 有工具调用 → 逐个执行，然后带着结果再来一轮
                if (res.ToolCalls.Count > 0)
                {
                    emptyReplies = 0;
                    history.Add(BuildAssistantToolCallMessage(res, bubble.Text));

                    // 中间轮的「正文」是模型调工具前的说明，不是给用户的最终答案。
                    // 折进「理解与计划」（默认收起），最终答案那一轮才以正文出现。
                    if (!string.IsNullOrEmpty(bubble.Text))
                    {
                        bubble.Plan = string.IsNullOrEmpty(bubble.Plan)
                            ? bubble.Text
                            : bubble.Plan + "\n\n" + bubble.Text;
                        bubble.Text = "";
                    }

                    foreach (var call in res.ToolCalls)
                    {
                        turnCt.ThrowIfCancellationRequested();

                        // 同一个工具 + 完全相同参数连着调 = 模型在原地打转。
                        // 不看返回值、不看模型解释，只看调用本身，最不容易被绕过去。
                        var callKey = call.Name + "|" + (call.Arguments ?? "");
                        identicalCalls = callKey == lastCallKey ? identicalCalls + 1 : 1;
                        lastCallKey = callKey;
                        if (identicalCalls >= MaxIdenticalCalls && stopReason == null)
                        {
                            stopReason = "同一个工具带着相同参数连续调用了 " + MaxIdenticalCalls + " 次（模型在原地打转）";
                        }

                        // 8 步用尽 / 判定死循环后，本轮剩下的调用仍必须给模型一条
                        // tool 回执，否则下一轮请求会因为缺 tool 消息被上游 400 拒掉。
                        if (executed >= StepCapacity || stopReason != null)
                        {
                            history.Add(new Dictionary<string, object?>
                            {
                                ["role"] = "tool",
                                ["tool_call_id"] = call.Id,
                                ["content"] = "（本步未执行：" + (stopReason ?? ("已达 " + StepCapacity + " 步上限"))
                                              + "。请直接给出结论，不要再调用工具。）",
                            });
                            continue;
                        }

                        var slot = StepSlots[executed];
                        var display = AiToolHost.DisplayName(call.Name);
                        var step = new AiStep(display + "：" + ShortArgs(call.Arguments),
                                              call.Arguments, bubble.Text.Length);
                        bubble.AddStep(step);
                        bubble.RefreshStepsHeader();
                        slot.Start(display);
                        SelectSlot(slot);
                        StatusText = "第 " + (executed + 1) + "/" + StepCapacity + " 步 · " + display + "…";

                        AiToolResult r;
                        try
                        {
                            r = await tools.ExecuteAsync(call.Name, call.Arguments, turnCt, ConfirmAsync);
                        }
                        catch (OperationCanceledException)
                        {
                            // 用户在这步中途点了停止 / 全局超时。
                            // 不在这里收尾的话，这一步会永远停在「执行中…」。
                            step.Finish(false, "已停止：这一步被中断。");
                            bubble.RefreshStepsHeader();
                            slot.Finish(false, "已停止：这一步被中断。");
                            RefreshStepRail();
                            throw;
                        }

                        step.Finish(r.Ok, r.Output);
                        bubble.RefreshStepsHeader();
                        slot.Finish(r.Ok, step.Detail);
                        executed++;
                        executedSteps = executed;   // 横排表头与真实执行次数同源
                        RefreshStepRail();

                        history.Add(new Dictionary<string, object?>
                        {
                            ["role"] = "tool",
                            ["tool_call_id"] = call.Id,
                            ["content"] = Clip(r.Output, ToolResultSendLimit),
                        });
                    }

                    TrimHistory();
                    if (stopReason != null) break;
                    if (executed >= StepCapacity)
                    {
                        stopReason = StepCapacity + " 个执行步骤已全部跑完";
                        break;
                    }
                    StatusText = "整理结果…";
                    continue;
                }

                // ④ 没有工具调用 = 最终答案。但「一个字都没有」不算答案：
                //    上游偶尔给一段空 SSE 就断开，以前会当成任务完成直接收工，
                //    看起来就是「没执行到 8 步就停了」。
                if (string.IsNullOrWhiteSpace(res.Content))
                {
                    emptyReplies++;
                    if (emptyReplies <= MaxEmptyReplies)
                    {
                        if (!bubble.HasText && !bubble.HasThinking && !bubble.HasSteps)
                            Messages.Remove(bubble);
                        StatusText = "等待模型输出…";
                        await Task.Delay(500, turnCt);
                        continue;
                    }
                    stopReason = "连续收到 " + MaxEmptyReplies + " 次空回复";
                    break;
                }

                history.Add(new Dictionary<string, object?>
                {
                    ["role"] = "assistant",
                    ["content"] = res.Content,
                });
                TrimHistory();

                var secs = (DateTime.UtcNow - started).TotalSeconds;
                StatusText = string.Format("完成 · {0:0.0}s · {1}/{2} 步", secs, executed, StepCapacity);
                return;
            }

            // 走到这里一定是某个硬闸门拦下来的（或死循环检测命中）。
            // 文案里写清楚「用了几步 / 为什么停」，别让玩家以为程序崩了。
            Messages.Add(new AiMessage(AiMessage.RoleNotice)
            {
                Text = "已停止：" + stopReason + "。本轮共执行 " + executed + "/" + StepCapacity
                       + " 步，为防死循环不会继续。可以把任务拆小一点再发，或直接说「继续」。",
            });
            StatusText = "已达安全上限";
        }
        catch (OperationCanceledException)
        {
            if (ct.IsCancellationRequested)
            {
                Messages.Add(new AiMessage(AiMessage.RoleNotice) { Text = "已停止。" });
                StatusText = "已停止";
            }
            else
            {
                Messages.Add(new AiMessage(AiMessage.RoleNotice)
                {
                    Text = "本轮达到 " + (int)TurnTimeout.TotalMinutes + " 分钟硬超时，已自动停止。",
                });
                StatusText = "已超时停止";
            }
        }
        catch (Exception ex)
        {
            Messages.Add(new AiMessage(AiMessage.RoleNotice) { Text = "⚠️ " + ex.Message });
            StatusText = "出错";
        }
        finally
        {
            // 无论怎么退出，所有气泡都必须收掉「正在输出」—— 这是那个卡住 bug 的根治点
            foreach (var b in bubbles) { b.IsStreaming = false; b.MarkDone(); }
            understanding.UnderstandingDone = true;
            // 横排上任何还亮着「执行中」的格子也必须落地，否则永远转圈
            AbortRunningSlots("本轮已结束，这一步没有继续执行。");

            ClearPendingConfirm(false);
            turnAllowedTools.Clear();
            turnToken = default;
            cts?.Dispose();
            cts = null;
            IsBusy = false;

            // 这一轮真实花掉多少额度只有服务端知道，收尾后回读一次
            _ = RefreshQuotaAsync();
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
        confirmCancelReg = turnToken.Register(() => ResolveConfirm(false));
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

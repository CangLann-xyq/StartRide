using StartRide.App.ViewModels.Ai;

namespace StartRide.App.ViewModels.Shell;

/// <summary>
/// MainViewModel 的 AI 助手部分。
///
/// 为什么单独一个文件、而不是塞进 MainViewModel.cs：
///   那个文件是从程序集反编译回来的，构造函数一长串注入参数（settings service /
///   coordinator / window service / dispatcher / logger），往里加一个页面 VM
///   等于同时改 DI 注册、构造签名和所有调用点 —— 风险远大于收益。
///   这里改成「懒创建」：第一次打开 AI 页面时才 new，不打开就完全不占资源
///   （也避免每次启动都建 HttpClient 和一个空消息列表）。
/// </summary>
public sealed partial class MainViewModel
{
    private AiAssistantViewModel? aiAssistantPage;

    public AiAssistantViewModel AiAssistantPage => aiAssistantPage ??= new AiAssistantViewModel();
}

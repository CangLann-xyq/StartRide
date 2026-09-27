namespace StartRide.App.Models;

/// <summary>
/// 界面上的「一份法律文件」条目：标题与说明已按当前语言解析完成。
///
/// 由 <see cref="StartRide.Core.LegalDocuments"/> 这份唯一清单投影而来，
/// 首次运行弹窗、设置页与阅读器共用同一个类型，避免几处各写一套绑定结构。
/// 正文本身不在条目里 —— 阅读器按 <see cref="Id"/> 回清单查资源名再读内嵌 md。
/// </summary>
public sealed record LegalDocumentItem(string Id, string Title, string Description);

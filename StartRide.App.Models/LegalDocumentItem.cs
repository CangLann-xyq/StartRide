namespace StartRide.App.Models;

/// <summary>
/// 界面上的「一份法律文件」条目：标题与说明已按当前语言解析完成，地址已按"线上优先、仓库兜底"取好。
///
/// 由 <see cref="StartRide.Core.LegalDocuments"/> 这份唯一清单投影而来，
/// 首次运行弹窗与设置页共用同一个类型，避免两处各写一套绑定结构。
/// </summary>
public sealed record LegalDocumentItem(string Id, string Title, string Description, string Url);

using System.Collections.Generic;
using StartRide.App.Resources;
using StartRide.Core;

namespace StartRide.App.Models;

/// <summary>
/// 把 <see cref="LegalDocuments"/> 那份与语言无关的清单，投影成界面上可以直接绑定的条目。
///
/// 拆一层的原因：核心程序集（<c>StartRide</c>）不该引用 App 层的资源类，
/// 所以清单里只存 resx 键名，真正取文案这一步放在这里。
/// </summary>
public static class LegalDocumentItemFactory
{
    /// <summary>首次运行弹窗要用户逐条过目的文件（不含"版权声明/开源协议"这类署名入口）。</summary>
    public static IReadOnlyList<LegalDocumentItem> CreateFirstRunAgreements()
        => Project(LegalDocuments.FirstRunAgreement);

    /// <summary>设置页「版权及法律声明」里的全部条目（顺序即展示顺序）。</summary>
    public static IReadOnlyList<LegalDocumentItem> CreateAll()
        => Project(LegalDocuments.All);

    private static IReadOnlyList<LegalDocumentItem> Project(IReadOnlyList<LegalDocument> documents)
    {
        var items = new List<LegalDocumentItem>(documents.Count);
        foreach (LegalDocument document in documents)
        {
            items.Add(new LegalDocumentItem(
                document.Id,
                ResolveText(document.TitleKey),
                ResolveText(document.DescriptionKey)));
        }

        return items;
    }

    /// <summary>
    /// 取文案；键缺失时返回**空串**而不是键名本身。
    ///
    /// ⚠️ 这里刻意不用键名兜底：万一新增文件时漏了 resx 条目，
    /// 界面上宁可少一行，也不要出现 "Legal_Doc_Xxx_Title" 这种把内部键名摊给用户看的情况。
    /// </summary>
    private static string ResolveText(string key)
        => string.IsNullOrWhiteSpace(key) ? string.Empty : Strings.GetOrDefault(key);
}

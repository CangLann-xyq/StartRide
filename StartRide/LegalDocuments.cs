using System;
using System.Collections.Generic;

namespace StartRide.Core
{
    /// <summary>
    /// 一份对外法律文件的定义（标题、说明、正文地址）。
    ///
    /// 标题与说明只存 <c>Strings.resx</c> 的**键名**，不存正文——这样 <c>StartRide</c> 核心程序集
    /// 不必依赖 App 层的资源类，同时四套语言（简体/繁體/英/日）都能各自翻译。
    /// </summary>
    public sealed class LegalDocument
    {
        public LegalDocument(string id, string titleKey, string descriptionKey, string url, bool showOnFirstRun)
        {
            Id = id;
            TitleKey = titleKey;
            DescriptionKey = descriptionKey;
            Url = url;
            ShowOnFirstRun = showOnFirstRun;
        }

        /// <summary>稳定标识（user-agreement / privacy-policy / …），用于日志与测试断言。</summary>
        public string Id { get; }

        /// <summary>标题的 resx 键名。</summary>
        public string TitleKey { get; }

        /// <summary>一句话说明的 resx 键名。</summary>
        public string DescriptionKey { get; }

        /// <summary>正文地址（腾讯文档优先，缺失时已在 <see cref="SiteLinks"/> 里退回仓库）。</summary>
        public string Url { get; }

        /// <summary>是否在「首次运行同意条款」弹窗里逐条列出。</summary>
        public bool ShowOnFirstRun { get; }
    }

    /// <summary>
    /// 对外法律文件的**唯一清单**。
    ///
    /// 背景：这类文件以前是三处各写各的——首次运行弹窗里一个超链接、设置页里三行硬编码
    /// <c>&lt;Grid&gt;</c>、仓库里一份 md，改一处漏两处。现在改成"清单驱动"：
    /// 弹窗和设置页都遍历 <see cref="All"/>，加一份文件只改这里一处。
    ///
    /// ⚠️ 新增文件时务必同时在四套 <c>Strings.*.resx</c> 里补上对应的两个键，
    ///    否则标题会退化成键名本身（界面上会出现 "Legal_Doc_Xxx_Title" 这种字面量）。
    /// </summary>
    public static class LegalDocuments
    {
        public static IReadOnlyList<LegalDocument> All { get; } = new[]
        {
            new LegalDocument(
                "user-agreement",
                "Legal_Doc_UserAgreement_Title",
                "Legal_Doc_UserAgreement_Description",
                SiteLinks.UserAgreementUrl,
                showOnFirstRun: true),

            new LegalDocument(
                "privacy-policy",
                "Legal_Doc_PrivacyPolicy_Title",
                "Legal_Doc_PrivacyPolicy_Description",
                SiteLinks.FirstNonEmpty(SiteLinks.Legal.PrivacyPolicyDoc, SiteLinks.Legal.PrivacyPolicyFallbackUrl),
                showOnFirstRun: true),

            new LegalDocument(
                "minor-protection",
                "Legal_Doc_MinorProtection_Title",
                "Legal_Doc_MinorProtection_Description",
                SiteLinks.FirstNonEmpty(SiteLinks.Legal.MinorProtectionDoc, SiteLinks.Legal.MinorProtectionFallbackUrl),
                showOnFirstRun: true),

            new LegalDocument(
                "disclaimer",
                "Legal_Doc_Disclaimer_Title",
                "Legal_Doc_Disclaimer_Description",
                SiteLinks.FirstNonEmpty(SiteLinks.Legal.DisclaimerDoc, SiteLinks.Legal.DisclaimerFallbackUrl),
                showOnFirstRun: true),

            new LegalDocument(
                "multiplayer-conduct",
                "Legal_Doc_MultiplayerConduct_Title",
                "Legal_Doc_MultiplayerConduct_Description",
                SiteLinks.FirstNonEmpty(SiteLinks.Legal.MultiplayerConductDoc, SiteLinks.Legal.MultiplayerConductFallbackUrl),
                showOnFirstRun: true),

            new LegalDocument(
                "third-party-notices",
                "Legal_Doc_ThirdPartyNotices_Title",
                "Legal_Doc_ThirdPartyNotices_Description",
                SiteLinks.FirstNonEmpty(SiteLinks.Legal.ThirdPartyNoticesDoc, SiteLinks.Legal.ThirdPartyNoticesFallbackUrl),
                showOnFirstRun: true),

            // 下面两份不是"同意对象"，只是合规署名入口，所以不进首次运行弹窗。
            // ⚠️ 两行都指向腾讯文档上那份《开源许可与版权声明》（GPL-3.0 全文 + 版权署名）：
            //    以前指向 GitHub 的仓库页与 LICENSE，而 GitHub 国内直连打不开。
            new LegalDocument(
                "copyright",
                "Legal_Doc_Copyright_Title",
                "Legal_Doc_Copyright_Description",
                SiteLinks.FirstNonEmpty(SiteLinks.Legal.OpenSourceLicenseDoc, SiteLinks.Legal.OpenSourceLicenseFallbackUrl),
                showOnFirstRun: false),

            new LegalDocument(
                "license",
                "Legal_Doc_License_Title",
                "Legal_Doc_License_Description",
                SiteLinks.FirstNonEmpty(SiteLinks.Legal.OpenSourceLicenseDoc, SiteLinks.Legal.OpenSourceLicenseFallbackUrl),
                showOnFirstRun: false),
        };

        /// <summary>首次运行弹窗要逐条列出的文件（用户需要明确同意的那些）。</summary>
        public static IReadOnlyList<LegalDocument> FirstRunAgreement { get; } = Filter(showOnFirstRun: true);

        /// <summary>按标识取一份文件；找不到返回 null（不抛异常，避免因为清单改动把界面打崩）。</summary>
        public static LegalDocument? Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            foreach (LegalDocument document in All)
            {
                if (string.Equals(document.Id, id, StringComparison.Ordinal))
                {
                    return document;
                }
            }

            return null;
        }

        private static IReadOnlyList<LegalDocument> Filter(bool showOnFirstRun)
        {
            var list = new List<LegalDocument>();
            foreach (LegalDocument document in All)
            {
                if (document.ShowOnFirstRun == showOnFirstRun)
                {
                    list.Add(document);
                }
            }

            return list;
        }
    }
}

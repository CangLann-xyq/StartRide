using System;

namespace StartRide.Core
{
    /// <summary>
    /// StartRide 对外链接的**唯一事实来源**。
    ///
    /// 背景：这个启动器是从一个开源 Minecraft 启动器反编译改造来的，原先界面里到处都是
    /// 它自己的痕迹——版权行署它的名、"查看 GitHub 仓库"跳它的仓库、反馈对话框指向它的
    /// discussions/issues、连"检查更新"都在拉它的 raw 清单。
    ///
    /// 现在所有对外地址都收敛到这里：换域名/换仓库只改这一个文件，不用满工程找字符串。
    /// </summary>
    public static class SiteLinks
    {
        /// <summary>项目主页（已备案的 StartRide 站点）。</summary>
        public const string ProjectHome = "https://startride.top";

        /// <summary>
        /// 联机中继的项目入口（「联机功能使用须知」弹窗里的「StartRide 中继项目」、联机页的归属行都指这里）。
        ///
        /// ⚠️ 这里原本硬编码指向上游那套 Minecraft 局域网穿透方案（Terracotta）的仓库，
        /// 但联机层早已换成自建中继（relay-server.js + 房间码），文案改了、链接却漏改，
        /// 会出现「写着 StartRide 中继项目、点开是别人的 Minecraft 项目」。
        /// 以后有独立的中继说明页，也只改这一行。
        /// </summary>
        public static string RelayProjectUrl => GitHubRepo;

        /// <summary>GitHub 仓库所有者。换账号只改这一行。</summary>
        public const string GitHubOwner = "CangLann-xyq";

        /// <summary>GitHub 仓库名。换仓库只改这一行。</summary>
        public const string GitHubRepoName = "StartRide";

        /// <summary>默认分支。</summary>
        public const string GitHubBranch = "main";

        public static string GitHubRepo => "https://github.com/" + GitHubOwner + "/" + GitHubRepoName;

        public static string GitHubReleases => GitHubRepo + "/releases";

        public static string GitHubIssues => GitHubRepo + "/issues";

        public static string GitHubDiscussions => GitHubRepo + "/discussions";

        public static string GitHubNewIssue => GitHubRepo + "/issues/new";

        /// <summary>
        /// 新建"新功能建议"。带 template 参数，GitHub 会直接打开对应表单。
        /// ⚠️ 用这两个地址的前提是仓库里存在 .github/ISSUE_TEMPLATE/ 下的同名模板，
        /// 模板缺失时 GitHub 会跳到"找不到模板"的错误页——改模板文件名务必同步这里。
        /// </summary>
        public static string GitHubNewFeatureRequest => GitHubNewIssue + "?template=feature_request.md";

        /// <summary>新建"Bug 反馈"（同上，依赖 .github/ISSUE_TEMPLATE/bug_report.md）。</summary>
        public static string GitHubNewBugReport => GitHubNewIssue + "?template=bug_report.md";

        /// <summary>仓库里的许可证文件。</summary>
        public static string LicenseUrl => GitHubRepo + "/blob/" + GitHubBranch + "/LICENSE";

        /// <summary>仓库里的用户协议（腾讯文档不可用时的兜底地址）。</summary>
        public static string UserAgreementFallbackUrl =>
            GitHubRepo + "/blob/" + GitHubBranch + "/docs/legal/01-user-agreement.md";

        /// <summary>
        /// 用户协议入口。
        ///
        /// ⚠️ 2026-09-26 起，**用户可见的法律文件正文统一托管在腾讯文档**（国内直连可开）。
        /// 原先进程里指向 GitHub blob，但 GitHub 在国内直连打不开——首次运行的「同意条款」
        /// 弹窗里那个链接点下去是白屏，等于用户看不到自己要同意什么，这是个真问题。
        /// 腾讯文档地址为空时自动退回 GitHub（开发期/未回填时不会出现死链）。
        /// </summary>
        public static string UserAgreementUrl =>
            FirstNonEmpty(Legal.UserAgreementDoc, UserAgreementFallbackUrl);

        /// <summary>
        /// 法律文件正文的托管地址（**腾讯文档**）与 GitHub 兜底地址。
        ///
        /// ⚠️ 六个 <c>*Doc</c> 常量由 <c>tools/_sr_tencent_legal.py</c> 自动回填，
        /// 请不要手改它们的格式；留空即表示"该文件尚未上云"，运行时会退回 GitHub。
        ///
        /// 为什么正文放腾讯文档而不是自建页面：
        ///   1. 腾讯文档国内直连可用，写一次即可在 Windows 客户端、手机、网页上看到同一份；
        ///   2. 法律文件需要能**独立于软件版本**更新（条款变更要能立刻生效），
        ///      放在软件包里等于每次改一个错别字都要发版；
        ///   3. 版本历史可追溯，便于举证"用户在何时同意的是哪一版"。
        /// </summary>
        public static class Legal
        {
            /// <summary>《StartRide 用户服务协议》</summary>
            public const string UserAgreementDoc = "https://docs.qq.com/doc/DUWJLR0tXVHdhZEVZ";

            /// <summary>《StartRide 隐私政策》</summary>
            public const string PrivacyPolicyDoc = "https://docs.qq.com/doc/DUWpBeHJza29DWGNi";

            /// <summary>《StartRide 未成年人个人信息保护规则》</summary>
            public const string MinorProtectionDoc = "https://docs.qq.com/doc/DUXVndXJ2cXFIam9P";

            /// <summary>《StartRide 免责声明与风险提示》</summary>
            public const string DisclaimerDoc = "https://docs.qq.com/doc/DUVBwQ3NWeGxxSkhM";

            /// <summary>《StartRide 联机服务使用规范》</summary>
            public const string MultiplayerConductDoc = "https://docs.qq.com/doc/DUUhXTUxnZnBIREVa";

            /// <summary>《StartRide 第三方组件与开源许可声明》</summary>
            public const string ThirdPartyNoticesDoc = "https://docs.qq.com/doc/DUWxMcmNodW1NVHZu";

            /// <summary>
            /// 《StartRide 开源许可与版权声明》（本项目自身的 GPL-3.0 全文 + 版权署名）。
            ///
            /// ⚠️ 「版权声明」「开源协议」这两行以前指向 GitHub 的仓库页和 LICENSE 文件，
            /// 而 GitHub 在国内直连打不开 —— 用户点下去是白屏，等于"看不到自己被告知的许可"。
            /// 现在统一指向腾讯文档上这份（正文由 <c>tools/_sr_gen_license_doc.py</c> 从根目录
            /// LICENSE 生成，不会与仓库脱节）。
            /// </summary>
            public const string OpenSourceLicenseDoc = "https://docs.qq.com/doc/DUWdiSFd2RFV5YU5O";

            /// <summary>法律文件总目录（首次运行弹窗与设置页都可以作为"看全部"的入口）。</summary>
            public const string IndexDoc = "https://docs.qq.com/doc/DUVVMRG1oRkVFbWJy";

            /// <summary>仓库里的隐私政策（兜底）。</summary>
            public static string PrivacyPolicyFallbackUrl =>
                GitHubRepo + "/blob/" + GitHubBranch + "/docs/legal/02-privacy-policy.md";

            /// <summary>仓库里的未成年人保护规则（兜底）。</summary>
            public static string MinorProtectionFallbackUrl =>
                GitHubRepo + "/blob/" + GitHubBranch + "/docs/legal/03-minor-protection.md";

            /// <summary>仓库里的免责声明（兜底）。</summary>
            public static string DisclaimerFallbackUrl =>
                GitHubRepo + "/blob/" + GitHubBranch + "/docs/legal/04-disclaimer.md";

            /// <summary>仓库里的联机规范（兜底）。</summary>
            public static string MultiplayerConductFallbackUrl =>
                GitHubRepo + "/blob/" + GitHubBranch + "/docs/legal/05-multiplayer-conduct.md";

            /// <summary>仓库里的第三方许可声明（兜底）。</summary>
            public static string ThirdPartyNoticesFallbackUrl =>
                GitHubRepo + "/blob/" + GitHubBranch + "/docs/legal/06-third-party-notices.md";

            /// <summary>仓库里的开源许可与版权声明（兜底）。</summary>
            public static string OpenSourceLicenseFallbackUrl =>
                GitHubRepo + "/blob/" + GitHubBranch + "/docs/legal/07-open-source-license.md";
        }

        /// <summary>返回第一个非空白字符串（用于"线上地址优先、仓库兜底"这种链式取值）。</summary>
        internal static string FirstNonEmpty(params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate.Trim();
                }
            }

            return string.Empty;
        }

        /// <summary>仓库里的更新清单目录（raw 直链，不用走跳转页）。</summary>
        public static string GitHubRawBase =>
            "https://raw.githubusercontent.com/" + GitHubOwner + "/" + GitHubRepoName + "/" + GitHubBranch;

        /// <summary>
        /// 自有更新源基址（更新清单 + 更新说明页）。
        ///
        /// ⚠️ 2026-09 从 windseek.cloud 迁到这里。windseek.cloud 只承担"服务器连接"
        /// （云同步 / 联机 API），**没有备案、之前被提醒过**；凡是有用户会打开的内容
        /// （更新说明页、安装包下载）都必须放在已备案的 startride.top 上，别再改回去。
        ///
        /// 服务器上仍然为 2.9.9 及更早的启动器保留了一份同内容的 JSON 清单
        /// （它们内置的是 windseek.cloud/update 地址），但那份只放 JSON，不放网页。
        /// </summary>
        public const string SelfHostedUpdateBase = "https://startride.top/update";

        /// <summary>版权署名行（关于页显示）。</summary>
        public const string CopyrightLine = "Copyright © 2026 肖又祺 · StartRide";

        /// <summary>产品名（对话框、协议等文案里统一用这个）。</summary>
        public const string ProductName = "StartRide 启动器";

        /// <summary>
        /// 反馈用的联系邮箱（无 GitHub 账号的用户也能反馈）。
        ///
        /// ⚠️ 当前**没有任何界面引用它**（反馈都走 <see cref="FeedbackPage"/> 站内表单）。
        /// 保留是为了让「联系邮箱」有唯一出处，改邮箱时和 README / docs/legal/ 下的法律文件一起改。
        /// </summary>
        public const string SupportEmail = "cloudfur2026@qq.com";

        /// <summary>
        /// 反馈页（站内表单，国内直连可用）。
        ///
        /// ⚠️ 启动器反馈弹窗的「新功能建议 / Bug 反馈」都指这里，不再指 GitHub Issues ——
        /// GitHub 在国内直连打不开（本机也是靠隧道才能推），让用户点开一个打不开的页面等于没有反馈入口。
        /// 反馈表单会把内容邮件给维护者，页面本身也给了邮件兜底。
        /// </summary>
        public const string FeedbackPage = "https://startride.top/feedback.html";

        /// <summary>按类型（feature / bug / other）拼出带当前版本号的反馈页地址。</summary>
        public static string FeedbackUrl(string type)
        {
            string t = string.IsNullOrWhiteSpace(type) ? "other" : type.Trim().ToLowerInvariant();
            return FeedbackPage + "?type=" + t + "&v=" + BuildInfo.Version;
        }

        /// <summary>按渠道拼出更新清单的候选地址（按顺序尝试，第一个成功就用）。</summary>
        public static string[] UpdateManifestCandidates(string channel)
        {
            string file = "launcher-" + (channel ?? "release").ToLowerInvariant() + ".json";
            return new[]
            {
                SelfHostedUpdateBase + "/" + file,
                GitHubRawBase + "/update/" + file,
            };
        }

        /// <summary>把外部链接统一收口（未来要加白名单/U TM 参数也在这里做）。</summary>
        public static bool IsOwnedHost(Uri uri)
        {
            if (uri == null) return false;
            string host = uri.Host.ToLowerInvariant();
            return host.EndsWith("startride.top", StringComparison.Ordinal)
                || host.EndsWith("windseek.cloud", StringComparison.Ordinal)
                || host.EndsWith("github.com", StringComparison.Ordinal)
                || host.EndsWith("githubusercontent.com", StringComparison.Ordinal);
        }
    }
}

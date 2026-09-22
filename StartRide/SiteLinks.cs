using System;

namespace StartRide.Core
{
    /// <summary>
    /// StartRide 对外链接的**唯一事实来源**。
    ///
    /// 背景：这个启动器是从 StartRide Launcher 反编译改造来的，原先界面里到处都是它的
    /// 痕迹——版权行写 "Copyright © 2026 StartRide Launcher 开发者"、"查看 GitHub 仓库"
    /// 跳到 zqq-699/StartRide-Launcher、反馈对话框指向它的 discussions/issues、
    /// 连"检查更新"都在拉它的 raw.githubusercontent 清单。
    ///
    /// 现在所有对外地址都收敛到这里：换域名/换仓库只改这一个文件，不用满工程找字符串。
    /// </summary>
    public static class SiteLinks
    {
        /// <summary>项目主页（已备案的 StartRide 站点）。</summary>
        public const string ProjectHome = "https://startride.top";

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

        /// <summary>仓库里的许可证文件。</summary>
        public static string LicenseUrl => GitHubRepo + "/blob/" + GitHubBranch + "/LICENSE";

        /// <summary>仓库里的用户协议。</summary>
        public static string UserAgreementUrl => GitHubRepo + "/blob/" + GitHubBranch + "/docs/USER-AGREEMENT.md";

        /// <summary>仓库里的更新清单目录（raw 直链，不用走跳转页）。</summary>
        public static string GitHubRawBase =>
            "https://raw.githubusercontent.com/" + GitHubOwner + "/" + GitHubRepoName + "/" + GitHubBranch;

        /// <summary>自有域名的更新清单目录（备用通道，服务器可放一份）。</summary>
        public const string SelfHostedUpdateBase = "https://windseek.cloud/update";

        /// <summary>版权署名行（关于页显示）。</summary>
        public const string CopyrightLine = "Copyright © 2026 肖又祺 · StartRide";

        /// <summary>产品名（对话框、协议等文案里统一用这个）。</summary>
        public const string ProductName = "StartRide 启动器";

        /// <summary>反馈用的联系邮箱（无 GitHub 账号的用户也能反馈）。</summary>
        public const string SupportEmail = "3956860183@qq.com";

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

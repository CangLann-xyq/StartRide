using System;

namespace StartRide.Core
{

    public static class SiteLinks
    {
        public const string ProjectHome = "https://startride.top";

        public static string RelayProjectUrl => GitHubRepo;

        public const string GitHubOwner = "CangLann-xyq";

        public const string GitHubRepoName = "StartRide";

        public const string GitHubBranch = "main";

        public static string GitHubRepo => "https://github.com/" + GitHubOwner + "/" + GitHubRepoName;

        public static string GitHubReleases => GitHubRepo + "/releases";

        public static string GitHubIssues => GitHubRepo + "/issues";

        public static string GitHubDiscussions => GitHubRepo + "/discussions";

        public static string GitHubNewIssue => GitHubRepo + "/issues/new";

        public static string GitHubNewFeatureRequest => GitHubNewIssue + "?template=feature_request.md";

        public static string GitHubNewBugReport => GitHubNewIssue + "?template=bug_report.md";

        public static string GitHubRawBase =>
            "https://raw.githubusercontent.com/" + GitHubOwner + "/" + GitHubRepoName + "/" + GitHubBranch;

        public const string SelfHostedUpdateBase = "https://startride.top/update";

        public const string CopyrightLine = "Copyright © 2026 肖又祺 · StartRide";

        public const string ProductName = "StartRide 启动器";

        public const string SupportEmail = "cloudfur2026@qq.com";

        public const string FeedbackPage = "https://startride.top/feedback.html";

        public static string FeedbackUrl(string type)
        {
            string t = string.IsNullOrWhiteSpace(type) ? "other" : type.Trim().ToLowerInvariant();
            return FeedbackPage + "?type=" + t + "&v=" + BuildInfo.Version;
        }

        public static string[] UpdateManifestCandidates(string channel)
        {
            string file = "launcher-" + (channel ?? "release").ToLowerInvariant() + ".json";
            return new[]
            {
                SelfHostedUpdateBase + "/" + file,
                GitHubRawBase + "/update/" + file,
            };
        }

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

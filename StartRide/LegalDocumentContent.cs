using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StartRide.Core
{
    /// <summary>
    /// 读取随程序集分发的法律文书正文。
    ///
    /// 2.11.0 起条款改为**在软件内浏览**：七份正文随包内嵌（csproj 的
    /// <c>EmbeddedResource</c>，逻辑名 <c>StartRide.App.Resources.Legal.&lt;文件名&gt;.md</c>），
    /// 点「查看」直接在阅读器里打开，不再跳到浏览器 / 腾讯文档。
    ///
    /// 取舍：正文从此与版本绑定 —— 改条款要发版。换来的是**离线可读、国内一定打得开**，
    /// 以及"用户同意的那一版"与包里那一版严格一致（条款托管在外站时，用户看到的
    /// 可能是几个月后被改过的内容，反而更难举证）。
    ///
    /// 读取失败一律返回空串而不抛异常：条款页面打不开可以补，启动器崩了没法补。
    /// </summary>
    public static class LegalDocumentContent
    {
        private static readonly object Gate = new();

        private static readonly Dictionary<string, string> Cache = new(StringComparer.Ordinal);

        /// <summary>取一份正文；资源名不对或找不到时返回空串。</summary>
        public static string Load(string? resourceName)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
            {
                return string.Empty;
            }

            string key = resourceName!.Trim();
            lock (Gate)
            {
                if (Cache.TryGetValue(key, out string? cached))
                {
                    return cached;
                }

                string content = ReadEmbedded(key);
                Cache[key] = content;
                return content;
            }
        }

        /// <summary>方便的写法：直接给清单里的一条记录。</summary>
        public static string Load(LegalDocument? document)
            => document is null ? string.Empty : Load(document.ResourceName);

        private static string ReadEmbedded(string resourceName)
        {
            try
            {
                using Stream? stream = typeof(LegalDocumentContent).Assembly.GetManifestResourceStream(resourceName);
                if (stream is null)
                {
                    return string.Empty;
                }

                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                return reader.ReadToEnd();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}

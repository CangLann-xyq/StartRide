using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StartRide.Core
{

    public static class LegalDocumentContent
    {
        private static readonly object Gate = new();

        private static readonly Dictionary<string, string> Cache = new(StringComparer.Ordinal);

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

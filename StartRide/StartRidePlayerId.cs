using System;
using System.Security.Cryptography;
using System.Text;

namespace StartRide.Core
{

    public static class StartRidePlayerId
    {
        public const string Prefix = "SR-";

        private const int BodyLength = 12;

        private const int GroupSize = 4;

        private const string Namespace = "StartRide/player-id/v1:";

        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        public static string Create(string? displayName)
        {
            string name = (displayName ?? string.Empty).Trim();

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(Namespace + name.ToLowerInvariant()));

            StringBuilder body = new StringBuilder(BodyLength);
            int accumulator = 0;
            int bits = 0;
            int index = 0;
            while (body.Length < BodyLength && index < hash.Length)
            {
                accumulator = (accumulator << 8) | hash[index++];
                bits += 8;
                while (bits >= 5 && body.Length < BodyLength)
                {
                    bits -= 5;
                    body.Append(Alphabet[(accumulator >> bits) & 31]);
                }
            }

            return Format(body.ToString());
        }

        public static bool IsValid(string? text) => TryNormalize(text, out _);

        public static bool IsUuidFormat(string? text) => text != null && TryNormalizeUuid(text, out _);

        public static bool TryNormalize(string? text, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string trimmed = text.Trim();

            if (TryNormalizeStartRideId(trimmed, out normalized))
            {
                return true;
            }

            if (TryNormalizeUuid(trimmed, out normalized))
            {
                return true;
            }

            if (TryNormalizeSteamId64(trimmed, out normalized))
            {
                return true;
            }

            return false;
        }

        private static bool TryNormalizeSteamId64(string text, out string normalized)
        {
            normalized = string.Empty;

            if (text.Length != 17)
            {
                return false;
            }
            foreach (char ch in text)
            {
                if (ch < '0' || ch > '9')
                {
                    return false;
                }
            }

            normalized = text;
            return true;
        }

        private static bool TryNormalizeStartRideId(string text, out string normalized)
        {
            normalized = string.Empty;

            StringBuilder compact = new StringBuilder(text.Length);
            foreach (char ch in text)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    compact.Append(ch);
                }
            }

            string raw = compact.ToString().ToUpperInvariant();
            if (!raw.StartsWith("SR", StringComparison.Ordinal))
            {
                return false;
            }

            raw = raw.Substring(2);
            if (raw.Length != BodyLength)
            {
                return false;
            }

            StringBuilder body = new StringBuilder(BodyLength);
            foreach (char ch in raw)
            {
                char fixedChar = ch switch
                {
                    'O' => '0',
                    'I' or 'L' => '1',
                    _ => ch,
                };

                if (Alphabet.IndexOf(fixedChar) < 0)
                {
                    return false;
                }
                body.Append(fixedChar);
            }

            normalized = Format(body.ToString());
            return true;
        }

        private static bool TryNormalizeUuid(string text, out string normalized)
        {
            normalized = string.Empty;

            if (text.Length != 36)
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                bool separatorPosition = i == 8 || i == 13 || i == 18 || i == 23;
                if (separatorPosition)
                {
                    if (ch != '-')
                    {
                        return false;
                    }
                    continue;
                }
                if (!Uri.IsHexDigit(ch))
                {
                    return false;
                }
            }

            normalized = text.ToLowerInvariant();
            return true;
        }

        private static string Format(string body)
        {
            StringBuilder result = new StringBuilder(Prefix.Length + body.Length + (body.Length / GroupSize));
            result.Append(Prefix);
            for (int i = 0; i < body.Length; i++)
            {
                if (i > 0 && i % GroupSize == 0)
                {
                    result.Append('-');
                }
                result.Append(body[i]);
            }
            return result.ToString();
        }
    }
}

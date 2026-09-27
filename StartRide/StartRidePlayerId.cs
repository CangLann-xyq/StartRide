using System;
using System.Security.Cryptography;
using System.Text;

namespace StartRide.Core
{
    /// <summary>
    /// StartRide 的联机 ID 生成与校验。
    ///
    /// 背景：这个启动器原本用的是 Minecraft 的"离线 UUID"算法，也就是
    /// <c>MD5("OfflinePlayer:" + 昵称)</c> 再把人家的 version/variant 位改一改。后果是
    /// StartRide 的玩家身份和 Minecraft 离线服务器的身份空间是**同一个**——同一个昵称在两边的
    /// ID 完全一样，界面上也一直写着"UUID"，看起来就是别的游戏的东西。
    ///
    /// StartRide 是 BeamNG.drive 的联机启动器，和 Minecraft 没有任何关系，所以这里改成自有的
    /// 命名空间与格式：
    ///
    /// <code>
    /// SR-7K3M-9PQX-B2HF
    /// </code>
    ///
    /// 规则：
    /// <list type="bullet">
    /// <item>算法：SHA-256("StartRide/player-id/v1:" + 小写昵称)，取高位 60 bit
    /// 编成 Crockford Base32（去掉容易看错的 I / L / O / U），固定 12 位正文。</item>
    /// <item>确定性：同一昵称在任何设备、任何版本上都得到同一个 ID，不需要联网、不落库。</item>
    /// <item>昵称大小写不影响身份——把 "Steve" 改成 "STEVE" 不该换一个玩家身份。</item>
    /// <item>写法宽容：<c>sr7k3m9pqxb2hf</c>、<c>SR-7K3M-9PQX-B2HF</c>、带空格都能识别，
    /// 统一规范化成上面那种分组大写写法。</item>
    /// </list>
    ///
    /// 为了不把已经存在的账号弄坏，<see cref="TryNormalize"/> 仍然接受标准 UUID
    /// （<c>8-4-4-4-12</c> 十六进制）：早期版本生成的就是 UUID，用户把它粘到"自定义"里
    /// 必须还能用，否则一按"应用"就会被判成格式错误。
    /// </summary>
    public static class StartRidePlayerId
    {
        /// <summary>联机 ID 的可读前缀，用来和别的系统一眼区分开。</summary>
        public const string Prefix = "SR-";

        /// <summary>正文位数（不含前缀与分隔符）。12 位 Crockford Base32 = 60 bit。</summary>
        private const int BodyLength = 12;

        /// <summary>每组的位数，只影响显示，解析时会被忽略。</summary>
        private const int GroupSize = 4;

        /// <summary>
        /// 哈希命名空间。带版本号是为了将来换算法时能平滑过渡：
        /// 换 v2 只需要改这一行，旧 ID 仍然能被 <see cref="TryNormalize"/> 识别。
        /// </summary>
        private const string Namespace = "StartRide/player-id/v1:";

        /// <summary>Crockford Base32 字母表——剔除了 I / L / O / U，避免和 1 / 0 看混。</summary>
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        /// <summary>
        /// 按昵称生成固定的联机 ID。
        /// </summary>
        /// <param name="displayName">玩家昵称。空白昵称也会得到一个稳定值，不会抛异常。</param>
        public static string Create(string? displayName)
        {
            string name = (displayName ?? string.Empty).Trim();

            // 先归一化大小写再哈希：改大小写属于"改写法"，不该换身份。
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

        /// <summary>判断是不是合法的联机 ID（自有格式或历史 UUID 都算）。</summary>
        public static bool IsValid(string? text) => TryNormalize(text, out _);

        /// <summary>
        /// 判断一个字符串是不是标准 UUID（<c>8-4-4-4-12</c> 十六进制）。
        /// 用来识别早期版本留下的、由 Minecraft 离线算法生成的联机 ID——
        /// 那些值需要被重算成 StartRide 自己的格式。
        /// </summary>
        public static bool IsUuidFormat(string? text) => text != null && TryNormalizeUuid(text, out _);

        /// <summary>
        /// 把用户输入整理成标准写法。接受：
        /// <list type="bullet">
        /// <item>StartRide 自有格式——大小写、分隔符随意，常见误写（O→0、I/L→1）也会被纠正；</item>
        /// <item>标准 UUID——早期版本生成的离线 ID 就是这个，规范化为小写后原样保留；</item>
        /// <item>SteamID64——Steam 账号的联机 ID 本来就是它（17 位纯数字），
        /// 用户切到"自定义"再点应用时必须能原样通过，否则好好的账号会被判成格式错误。</item>
        /// </list>
        /// </summary>
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

        /// <summary>SteamID64：17 位十进制数字，作为 Steam 账号的联机 ID 使用。</summary>
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

            // 去掉所有分隔符，只留字母数字，再剥掉前缀。
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
                // Crockford 的容错：这几个字母和数字长得像，按数字收下。
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

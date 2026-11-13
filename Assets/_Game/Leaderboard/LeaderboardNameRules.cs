using System.Text;

namespace FluffyClimbers.Leaderboard
{
    /// <summary>The verdict of <see cref="LeaderboardNameRules.Validate"/>.</summary>
    public enum LeaderboardNameCheck { Ok, TooShort, TooLong, BadCharacters, Blocked }

    /// <summary>
    /// The player's own leaderboard display name (owner 2026-11-13): 3 to 16 characters; letters of any language, digits, single spaces between words and underscores;
    /// no uniqueness check; a small built-in word filter. Pure and deterministic (no Unity, no network, no allocation outside the call).
    /// </summary>
    public static class LeaderboardNameRules
    {
        public const int MinLength = 3;
        public const int MaxLength = 16;

        // Matched anywhere inside the name (after leetspeak and separators are removed): long, unambiguous words only, so ordinary names are not caught.
        static readonly string[] Contains =
        {
            "fuck", "shit", "bitch", "cunt", "whore", "pussy", "nigger", "nigga", "faggot", "rapist", "asshole", "bastard", "dickhead", "cocksucker", "wanker", "retard", "porn"
        };

        // Matched only as a whole word (a space or underscore separated part): short words that sit inside many harmless words.
        static readonly string[] WholeWords =
        {
            "ass", "sex", "fag", "cum", "tit", "tits", "dick", "cock", "penis", "vagina", "slut", "rape", "nazi", "hitler", "kkk", "anus", "dildo", "boob", "boobs", "fuk", "sht"
        };

        /// <summary>Trims, turns every run of white space into one space. Null gives "". Letters keep their case (the name is shown as typed).</summary>
        public static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var sb = new StringBuilder(raw.Length);
            bool pendingSpace = false;
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (char.IsWhiteSpace(c)) { pendingSpace = sb.Length > 0; continue; }
                if (pendingSpace) { sb.Append(' '); pendingSpace = false; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Checks a name (it is normalised first). An empty name is TooShort: the caller treats "empty" as "go back to the Play Games name" before calling this.</summary>
        public static LeaderboardNameCheck Validate(string raw)
        {
            string n = Normalize(raw);
            for (int i = 0; i < n.Length; i++)
            {
                char c = n[i];
                if (!(char.IsLetter(c) || char.IsDigit(c) || c == ' ' || c == '_')) return LeaderboardNameCheck.BadCharacters;
            }
            if (n.Length < MinLength) return LeaderboardNameCheck.TooShort;
            if (n.Length > MaxLength) return LeaderboardNameCheck.TooLong;
            if (IsBlocked(n)) return LeaderboardNameCheck.Blocked;
            return LeaderboardNameCheck.Ok;
        }

        /// <summary>The short sentence shown under the field for a verdict ("" for Ok).</summary>
        public static string Message(LeaderboardNameCheck check)
        {
            switch (check)
            {
                case LeaderboardNameCheck.TooShort: return "Use at least " + MinLength + " characters.";
                case LeaderboardNameCheck.TooLong: return "Use at most " + MaxLength + " characters.";
                case LeaderboardNameCheck.BadCharacters: return "Letters, numbers, spaces and _ only.";
                case LeaderboardNameCheck.Blocked: return "Please pick a friendlier name.";
                default: return "";
            }
        }

        /// <summary>The name to show: the player's own name when it is set, else the fallback (the Play Games name).</summary>
        public static string Resolve(string own, string fallback)
        {
            string n = Normalize(own);
            return n.Length > 0 ? n : (fallback ?? "");
        }

        /// <summary>True when the name contains a word of the filter (leetspeak 0 1 3 4 5 7 @ $ ! is read as letters; spaces and underscores are ignored for the long words).</summary>
        public static bool IsBlocked(string name)
        {
            string lower = Fold(name);
            if (lower.Length == 0) return false;

            // 1) long words inside the whole text, separators removed, also with stretched letters collapsed ("fuuuck")
            var joined = new StringBuilder(lower.Length);
            for (int i = 0; i < lower.Length; i++) if (lower[i] != ' ' && lower[i] != '_') joined.Append(lower[i]);
            string j = joined.ToString();
            string j1 = Collapse(j, 1), j2 = Collapse(j, 2);   // stretched letters: "fuuuck" -> "fuck", "niggger" -> "nigger"
            for (int i = 0; i < Contains.Length; i++)
            {
                string w = Contains[i];
                if (j.Contains(w) || j2.Contains(w)) return true;
                if (Collapse(w, 1) == w && j1.Contains(w)) return true;   // a word with no double letter may also be stretched more than twice
            }

            // 2) short words only as whole parts
            var part = new StringBuilder();
            for (int i = 0; i <= lower.Length; i++)
            {
                char c = i < lower.Length ? lower[i] : ' ';
                if (c != ' ' && c != '_') { part.Append(c); continue; }
                if (part.Length > 0)
                {
                    string word = part.ToString();
                    for (int k = 0; k < WholeWords.Length; k++) if (word == WholeWords[k]) return true;
                    part.Length = 0;
                }
            }
            return false;
        }

        static string Fold(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = char.ToLowerInvariant(s[i]);
                switch (c)
                {
                    case '0': c = 'o'; break;
                    case '1': case '!': case '|': c = 'i'; break;
                    case '3': c = 'e'; break;
                    case '4': case '@': c = 'a'; break;
                    case '5': case '$': c = 's'; break;
                    case '7': c = 't'; break;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Shortens every run of the same character to at most <paramref name="max"/>.</summary>
        static string Collapse(string s, int max)
        {
            var sb = new StringBuilder(s.Length);
            int run = 0;
            for (int i = 0; i < s.Length; i++)
            {
                run = i > 0 && s[i] == s[i - 1] ? run + 1 : 1;
                if (run <= max) sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }
}

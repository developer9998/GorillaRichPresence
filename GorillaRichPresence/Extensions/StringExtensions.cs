using System.Linq;

namespace GorillaRichPresence.Extensions
{
    public static class StringExtensions
    {
        public static string ToTitleCase(this string str) => string.Concat(str.ToUpper()[0], str.ToLower()[1..]);
        public static string ToBestMatch(this string str, params string[] items) => items.FirstOrDefault(str.Contains) ?? str;
    }
}

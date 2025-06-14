using System.Globalization;

namespace GorillaRichPresence.Extensions
{
    internal static class StringEx
    {
        public static readonly TextInfo textInfo = CultureInfo.InvariantCulture.TextInfo;

        public static string ToTitleCase(this string str) => textInfo.ToTitleCase(str.ToLower());
    }
}

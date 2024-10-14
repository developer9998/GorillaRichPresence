using System.Globalization;

namespace GorillaRichPresence.Extensions
{
    internal static class StringEx
    {
        public static readonly TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

        public static string ToTitleCase(this string str) => textInfo.ToTitleCase(str);
    }
}

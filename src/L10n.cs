using System;
using System.Globalization;

namespace GearBatt
{
    internal static class L10n
    {
        public static bool Chinese = true;

        public static void Initialize(string language)
        {
            if (!string.IsNullOrEmpty(language))
            {
                string value = language.Trim().ToLowerInvariant();
                if (value.StartsWith("en"))
                {
                    Chinese = false;
                    return;
                }
                if (value.StartsWith("zh"))
                {
                    Chinese = true;
                    return;
                }
            }

            // CurrentUICulture can be overridden by the process environment
            // (for example LANG). Use the Windows display language instead.
            ushort langId = Native.GetUserDefaultUILanguage();
            if (langId != 0)
            {
                Chinese = (langId & 0x03FF) == 0x0004; // LANG_CHINESE
                return;
            }

            Chinese = string.Equals(
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                "zh", StringComparison.OrdinalIgnoreCase);
        }

        public static string T(string chinese, string english)
        {
            return Chinese ? chinese : english;
        }

        public static string Source(string source)
        {
            if (!Chinese && source == "蓝牙")
                return "Bluetooth";
            if (!Chinese && source == "无线")
                return "Wireless";
            return source;
        }
    }
}

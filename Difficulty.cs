using I2.Loc;

namespace GlassSol
{
    internal static class Difficulty
    {
        public const int GlassSol = 2;
        public const int TrueGlassSol = 3;

        public static int Current()
        {
            PlayerGamePlayData data = PlayerGamePlayData.Instance;
            if (data == null || data.gameMode == null)
                return -1;

            return data.gameMode.CurrentValue;
        }

        public static bool IsTrueGlassSol()
        {
            return Current() == TrueGlassSol;
        }

        public static bool IsOneShot()
        {
            int mode = Current();
            return mode == GlassSol || mode == TrueGlassSol;
        }

        public static string Phrase(string russian, string english)
        {
            string code = LocalizationManager.CurrentLanguageCode;
            if (!string.IsNullOrEmpty(code) && code.StartsWith("ru", System.StringComparison.OrdinalIgnoreCase))
                return russian;

            string language = LocalizationManager.CurrentLanguage;
            if (language == "Russian" || language == "Русский")
                return russian;

            return english;
        }
    }
}

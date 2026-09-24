namespace GlassSol
{
    internal static class Difficulty
    {
        public const int GlassSol = 2;
        public const int TrueGlassSol = 3;

        public static bool IsOneShot()
        {
            PlayerGamePlayData data = PlayerGamePlayData.Instance;
            if (data == null || data.gameMode == null)
                return false;

            int mode = data.gameMode.CurrentValue;
            return mode == GlassSol || mode == TrueGlassSol;
        }
    }
}

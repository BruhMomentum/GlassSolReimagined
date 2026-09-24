using BepInEx;
using BepInEx.Logging;
using GlassSol.Patches;
using HarmonyLib;

namespace GlassSol
{
    [BepInPlugin(ModGuid, "GlassSol", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private const string ModGuid = "flyingRozenbaum.GlassSol";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = BepInEx.Logging.Logger.CreateLogSource(ModGuid);
            Log.LogInfo("Starting Glass Sol");

            Harmony.CreateAndPatchAll(typeof(Plugin).Assembly);

            Log.LogInfo("Finished loading Glass Sol");
        }
    }
}

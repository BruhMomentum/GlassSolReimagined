using System;
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

            Harmony harmony = new Harmony(ModGuid);
            Patch(harmony, typeof(PlayerHealthPatch));
            Patch(harmony, typeof(ModeMenuPatch));
            Patch(harmony, typeof(NewGameModeSavePatch));
            Patch(harmony, typeof(SaveModeLabelPatch));
            Patch(harmony, typeof(GameplaySettingPatch));

            Log.LogInfo("Finished loading Glass Sol");
        }

        static void Patch(Harmony harmony, Type type)
        {
            try
            {
                harmony.PatchAll(type);
                Log.LogInfo("Patched " + type.Name);
            }
            catch (Exception exception)
            {
                Log.LogError("Patch failed for " + type.Name + ": " + exception);
            }
        }
    }
}

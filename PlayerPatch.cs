using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GlassSol.Patches
{
    internal static class PlayerHealthPatch
    {
        static void Kill(PlayerHealth health)
        {
            if (health.IsDead || (!Difficulty.IsOneShot() && !NohitSetting.Active()))
                return;

            if (Difficulty.IsTrueGlassSol())
            {
                DeleteSaveAndReturnToMenu();
                return;
            }

            PlayDeath(health);
        }

        static bool InternalShouldKill()
        {
            return InternalKillSetting.Shown() && InternalKillSetting.Kills();
        }

        static void KeepAlive(PlayerHealth health)
        {
            float floor = health.maxHealth.Value * 0.01f;
            if (floor < 1f)
                floor = 1f;
            if (health.currentValue < floor)
                health.currentValue = floor;
        }

        static void PlayDeath(PlayerHealth health)
        {
            Player player = Player.i;
            health.currentValue = 0f;
            health.Die();
            if (player != null && player.health == health)
                player.DeadCheck(true);

            GameObject anim = new GameObject("GlassSolDeathAnim");
            Object.DontDestroyOnLoad(anim);
            anim.AddComponent<DeathAnim>();
        }

        internal static void DeleteSaveAndReturnToMenu()
        {
            SaveManager manager = SaveManager.Instance;
            int slot = (int)AccessTools.Field(typeof(SaveManager), "currentSlotIndex").GetValue(manager);
            if (slot >= 0)
                AccessTools.Method(typeof(SaveManager), "DeleteSave").Invoke(manager, new object[] { slot });

            Time.timeScale = 1f;
            ApplicationUIGroupManager ui = ApplicationUIGroupManager.Instance;
            if (ui != null && ui.blackCover != null)
            {
                Color color = ui.blackCover.color;
                color.a = 0f;
                ui.blackCover.color = color;
                ui.blackCover.gameObject.SetActive(false);
            }

            GameObject reveal = new GameObject("GlassSolRevealMenu");
            Object.DontDestroyOnLoad(reveal);
            reveal.AddComponent<RevealMenu>();
            ApplicationCore.Instance.QuitToMenu();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerAbilityData), "get_IsActivated")]
        static bool DisableJades(PlayerAbilityData __instance, ref bool __result)
        {
            if (!Difficulty.IsOneShot())
                return true;

            Player player = Player.i;
            if (player == null)
                return true;

            PlayerMainAbilityCollection abilities = player.mainAbilities;
            if (__instance != abilities.ReviveJade.AbilityData && __instance != abilities.EndureFooExplodeJade)
                return true;

            __result = false;
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.ReceiveDamage))]
        static void ReceiveDamage(PlayerHealth __instance, DamageDealer damageDealer, bool __result)
        {
            if (!__result || __instance.IsInvincible || damageDealer == null || damageDealer.DamageAmount <= 0f)
                return;

            Kill(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.ReceiveDOT_Damage))]
        static void ReceiveDotDamage(PlayerHealth __instance, float damageValue)
        {
            if (__instance.IsInvincible || damageValue <= 0f)
                return;

            Kill(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.ReceiveRecoverableDamage))]
        static void ReceiveRecoverableDamage(PlayerHealth __instance, float damage)
        {
            if (damage <= 0f || __instance.IsDead)
                return;

            if (!InternalShouldKill())
            {
                if (Difficulty.IsOneShot())
                    KeepAlive(__instance);
                return;
            }

            PlayDeath(__instance);
            if (Difficulty.IsTrueGlassSol())
            {
                GameObject wipe = new GameObject("GlassSolInternalDeath");
                Object.DontDestroyOnLoad(wipe);
                wipe.AddComponent<InternalDeathWipe>();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.CoughHurt))]
        static void CoughHurt(PlayerHealth __instance, float damageValue)
        {
            if (damageValue <= 0f)
                return;

            Kill(__instance);
        }
    }

    internal class DeathAnim : MonoBehaviour
    {
        bool played;
        int frames = 8;

        void LateUpdate()
        {
            Player player = Player.i;
            if (player != null && player.animator != null)
            {
                player.animator.SetLayerWeight(1, 0f);
                if (!played)
                {
                    player.animator.Play("Die", 0, 0f);
                    played = true;
                }
            }

            frames--;
            if (frames <= 0)
                Destroy(gameObject);
        }
    }

    internal class InternalDeathWipe : MonoBehaviour
    {
        float left = 1.4f;

        void Update()
        {
            left -= Time.unscaledDeltaTime;
            if (left > 0f)
                return;

            PlayerHealthPatch.DeleteSaveAndReturnToMenu();
            Destroy(gameObject);
        }
    }

    internal class RevealMenu : MonoBehaviour
    {
        int left = 90;

        void LateUpdate()
        {
            Time.timeScale = 1f;
            if (SceneManager.GetActiveScene().name != "TitleScreenMenu" && Object.FindObjectOfType<StartMenuLogic>() == null)
                return;

            ApplicationUIGroupManager ui = ApplicationUIGroupManager.Instance;
            if (ui != null && ui.blackCover != null)
            {
                Color color = ui.blackCover.color;
                color.a = 0f;
                ui.blackCover.color = color;
                ui.blackCover.raycastTarget = false;
                ui.blackCover.gameObject.SetActive(false);
            }

            left--;
            if (left <= 0)
                Destroy(gameObject);
        }
    }
}

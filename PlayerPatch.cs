using HarmonyLib;

namespace GlassSol.Patches
{
    internal static class PlayerHealthPatch
    {
        static void Kill(PlayerHealth health)
        {
            if (!Difficulty.IsOneShot() || health.IsDead)
                return;

            health.Die();

            Player player = Player.i;
            if (player != null && player.health == health)
                player.DeadCheck(true);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.ReceiveDamage))]
        static void ReceiveDamage(PlayerHealth __instance, DamageDealer damageDealer)
        {
            if (__instance.IsInvincible || damageDealer == null || damageDealer.DamageAmount <= 0f)
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
            if (damage <= 0f)
                return;

            Kill(__instance);
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
}

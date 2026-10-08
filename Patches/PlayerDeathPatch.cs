namespace Subnautica.MoreTpCommands.Patches
{
    using System;
    using HarmonyLib;
    using Subnautica.API.Features;
    using Subnautica.MoreTpCommands.Core;

    [HarmonyPatch(typeof(global::Player), nameof(global::Player.OnKill))]
    public static class PlayerDeathPatch
    {
        [HarmonyPriority(Priority.High)]
        private static void Prefix(global::Player __instance, DamageType damageType)
        {
            try
            {
                if (__instance != null && __instance == global::Player.main)
                {
                    DeathTracker.RecordDeath(__instance.transform.position);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error in PlayerDeathPatch: {ex}");
            }
        }
    }
}

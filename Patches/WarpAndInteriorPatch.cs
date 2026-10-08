namespace Subnautica.MoreTpCommands.Patches
{
    using System;
    using HarmonyLib;
    using Subnautica.API.Features;
    using Subnautica.Events.Patches.Fixes.Vehicle;
    using Subnautica.MoreTpCommands.Core;
    using UnityEngine;

    [HarmonyPatch(typeof(global::BaseEntranceTrigger), "OnTriggerExit")]
    public static class BaseEntranceTriggerExitPatch
    {
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(global::BaseEntranceTrigger __instance, Collider col)
        {
            try
            {
                var player = global::Player.main;
                if (__instance == null || col == null || player == null)
                {
                    return true;
                }

                if (TeleportUtility.ShouldBlockInteriorTrigger(
                    __instance.transform.position,
                    player.transform.position,
                    isExitEvent: true))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error in BaseEntranceTriggerExitPatch: {ex}");
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(global::BaseEntranceTrigger), "OnTriggerStay")]
    public static class BaseEntranceTriggerStayPatch
    {
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(global::BaseEntranceTrigger __instance, Collider col)
        {
            try
            {
                var player = global::Player.main;
                if (__instance == null || col == null || player == null)
                {
                    return true;
                }

                if (TeleportUtility.ShouldBlockInteriorTrigger(
                    __instance.transform.position,
                    player.transform.position,
                    isExitEvent: false))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error in BaseEntranceTriggerStayPatch: {ex}");
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(global::PrecursorMoonPoolTrigger), "Update")]
    public static class PrecursorMoonPoolTriggerUpdatePatch
    {
        [HarmonyPriority(Priority.High)]
        private static void Prefix(global::PrecursorMoonPoolTrigger __instance)
        {
            try
            {
                TeleportUtility.HandlePrecursorMoonPoolTriggerUpdate(__instance);
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error in PrecursorMoonPoolTriggerUpdatePatch: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(SeaTruckCustomMotorGeneral), nameof(SeaTruckCustomMotorGeneral.ChangeSeaTruckMode))]
    public static class SeaTruckCustomMotorChangeModePatch
    {
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(global::SeaTruckSegment seaTruck, bool isInSeaTruck)
        {
            try
            {
                if (isInSeaTruck && TeleportUtility.ShouldBlockSeaTruckModeActivation(seaTruck))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error in SeaTruckCustomMotorChangeModePatch: {ex}");
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(global::Player), "OnConsoleCommand_warp")]
    public static class PlayerWarpConsoleCommandPatch
    {
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(global::Player __instance, NotificationCenter.Notification n)
        {
            try
            {
                if (n != null && n.data != null && n.data.Count == 3)
                {
                    string xStr = n.data[0]?.ToString();
                    string yStr = n.data[1]?.ToString();
                    string zStr = n.data[2]?.ToString();

                    if (TeleportUtility.TryParseWarpCoordinates(new string[] { xStr, yStr, zStr }, out Vector3 targetPos))
                    {
                        TeleportUtility.SafeTeleport(targetPos);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error in PlayerWarpConsoleCommandPatch: {ex}");
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(global::GotoConsoleCommand), nameof(global::GotoConsoleCommand.GotoPosition))]
    public static class GotoConsoleCommandGotoPositionPatch
    {
        [HarmonyPriority(Priority.High)]
        private static void Prefix(Vector3 position)
        {
            try
            {
                var player = global::Player.main;
                if (player != null)
                {
                    TeleportUtility.MarkTeleportTimestamp(player.transform.position, position, targetIsExterior: true);
                    TeleportUtility.PreparePlayerForTeleport(player, position, stopGotoCoroutines: false);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error in GotoConsoleCommandGotoPositionPatch.Prefix: {ex}");
            }
        }

        [HarmonyPostfix]
        private static void Postfix(global::GotoConsoleCommand __instance, Vector3 position)
        {
            try
            {
                var player = global::Player.main;
                if (player != null)
                {
                    player.StartCoroutine(TeleportUtility.WaitForGotoAndFinalizeCoroutine(player, __instance, position));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error in GotoConsoleCommandGotoPositionPatch.Postfix: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(global::Player), nameof(global::Player.OnPlayerPositionCheat))]
    public static class PlayerPositionCheatPatch
    {
        [HarmonyPriority(Priority.High)]
        private static void Prefix(global::Player __instance)
        {
            try
            {
                // Vanilla OnPlayerPositionCheat sets this.currentSub = null directly without calling SetCurrentSub(null),
                // which skips SubRoot.OnPlayerExited and Multiplayer's ExitedSubroot event.
                if (__instance != null && __instance.GetCurrentSub() != null)
                {
                    __instance.SetCurrentSub(null);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error in PlayerPositionCheatPatch: {ex}");
            }
        }
    }
}

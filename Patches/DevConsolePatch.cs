namespace Subnautica.MoreTpCommands.Patches
{
    using System;
    using HarmonyLib;
    using Subnautica.API.Features;
    using Subnautica.MoreTpCommands.Core;

    [HarmonyPatch(typeof(global::DevConsole), "Awake")]
    public static class DevConsoleAwakePatch
    {
        [HarmonyPostfix]
        private static void Postfix(global::DevConsole __instance)
        {
            try
            {
                CommandHistory.SyncWithDevConsoleOnAwake(__instance);
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error in DevConsoleAwakePatch: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(global::DevConsole), "Submit")]
    public static class DevConsolePatch
    {
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(global::DevConsole __instance, string value, ref bool __result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            try
            {
                string trimmed = value.Trim();
                string effectiveCommand = trimmed.StartsWith("/") ? trimmed.Substring(1).TrimStart() : trimmed;

                if (string.IsNullOrEmpty(effectiveCommand))
                {
                    return true;
                }

                int firstSpace = effectiveCommand.IndexOfAny(new char[] { ' ', '\t' });
                string cmd = firstSpace >= 0 ? effectiveCommand.Substring(0, firstSpace) : effectiveCommand;
                cmd = cmd.ToLowerInvariant();

                // Record command into unified command history without syncing back to DevConsole (DevConsole is already the source)
                CommandHistory.Add(effectiveCommand, syncToDevConsole: false);

                if (cmd == "back" || cmd == "warp")
                {
                    bool wasHandled = CommandManager.HandleSubmitCommand(effectiveCommand);
                    if (wasHandled)
                    {
                        if (__instance != null)
                        {
                            __instance.SetState(false);
                            global::DevConsole.SetUsedConsole(true);
                        }

                        if (global::Player.main != null)
                        {
                            global::Player.main.hasUsedConsole = true;
                        }

                        __result = true;
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error in DevConsolePatch: {ex}");
            }

            return true;
        }

        [HarmonyPostfix]
        private static void Postfix(global::DevConsole __instance, string value)
        {
            try
            {
                CommandHistory.NormalizeDevConsoleLastHistoryEntry();
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error normalizing DevConsole history in Postfix: {ex}");
            }
        }
    }
}

namespace Subnautica.MoreTpCommands.Patches
{
    using System;
    using HarmonyLib;
    using Subnautica.API.Features;
    using Subnautica.MoreTpCommands.Core;
    using UnityEngine;
    using UnityEngine.EventSystems;

    [HarmonyPatch(typeof(global::ChatBoxManager), "Update")]
    public static class ChatBoxUpdatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(global::ChatBoxManager __instance)
        {
            if (__instance == null || !__instance.IsActive)
            {
                return;
            }

            if (__instance.TextInputField == null)
            {
                return;
            }

            bool isFieldFocused = __instance.TextInputField.isFocused ||
                (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == __instance.TextInputField.gameObject);

            if (!isFieldFocused)
            {
                return;
            }

            try
            {
                if (GameInput.GetKeyDown(KeyCode.UpArrow))
                {
                    CommandHistory.NavigateChatUp(__instance.TextInputField);
                }
                else if (GameInput.GetKeyDown(KeyCode.DownArrow))
                {
                    CommandHistory.NavigateChatDown(__instance.TextInputField);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error in ChatBoxUpdatePatch: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(global::ChatBoxManager), "Show")]
    public static class ChatBoxShowPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            CommandHistory.ResetNavigation();
        }
    }

    [HarmonyPatch(typeof(global::ChatBoxManager), "Hide")]
    public static class ChatBoxHidePatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            CommandHistory.ResetNavigation();
        }
    }

    [HarmonyPatch(typeof(global::ChatBoxManager), "OnInputFieldValueEndEdit")]
    public static class ChatBoxEndEditPatch
    {
        [HarmonyPrefix]
        private static void Prefix(global::ChatBoxManager __instance, string inputText)
        {
            if (__instance == null || __instance.TextInputField == null)
            {
                return;
            }

            try
            {
                string text = !string.IsNullOrEmpty(__instance.TextInputField.text)
                    ? __instance.TextInputField.text.Trim()
                    : (inputText?.Trim() ?? string.Empty);

                if (!string.IsNullOrEmpty(text) && __instance.IsPressedEnter())
                {
                    CommandHistory.OnChatSubmitted(text);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error in ChatBoxEndEditPatch: {ex}");
            }
        }
    }
}

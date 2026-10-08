namespace Subnautica.MoreTpCommands.Core
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using HarmonyLib;
    using Subnautica.API.Features;
    using UnityEngine.UI;

    public static class CommandHistory
    {
        public const int DefaultMaxHistory = 50;

        private static readonly List<string> history = new List<string>();
        private static int currentIndex = 0;
        private static string cachedDraft = string.Empty;

        // Reflection caches for syncing with DevConsole
        private static FieldInfo devConsoleInstanceField;
        private static FieldInfo devConsoleHistoryField;
        private static FieldInfo devConsoleInputField;
        private static FieldInfo consoleInputHistoryIndexField;
        private static bool reflectionInitialized = false;

        public static int MaxHistory { get; set; } = DefaultMaxHistory;

        public static IReadOnlyList<string> History => history;

        public static int CurrentIndex => currentIndex;

        public static string CachedDraft => cachedDraft;

        public static void Initialize()
        {
            InitReflection();
            ResetNavigation();
            LoadInitialHistory();
        }

        private static void InitReflection()
        {
            if (reflectionInitialized)
            {
                return;
            }

            try
            {
                devConsoleInstanceField = AccessTools.Field(typeof(global::DevConsole), "instance");
                devConsoleHistoryField = AccessTools.Field(typeof(global::DevConsole), "history");
                devConsoleInputField = AccessTools.Field(typeof(global::DevConsole), "inputField");
                var consoleInputType = AccessTools.TypeByName("ConsoleInput");
                if (consoleInputType != null)
                {
                    consoleInputHistoryIndexField = AccessTools.Field(consoleInputType, "historyIndex");
                }
                reflectionInitialized = true;
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Failed to initialize DevConsole reflection fields: {ex}");
            }
        }

        public static void LoadInitialHistory()
        {
            try
            {
                InitReflection();

                var devInstance = devConsoleInstanceField?.GetValue(null) as global::DevConsole;
                if (devInstance != null)
                {
                    var devHist = devConsoleHistoryField?.GetValue(devInstance) as List<string>;
                    if (devHist != null && devHist.Count > 0)
                    {
                        foreach (var item in devHist)
                        {
                            if (!string.IsNullOrWhiteSpace(item))
                            {
                                Add(item.Trim(), syncToDevConsole: false);
                            }
                        }
                    }
                }

                string consoleHistory = MiscSettings.consoleHistory;
                if (!string.IsNullOrEmpty(consoleHistory))
                {
                    string[] lines = consoleHistory.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            Add(line.Trim(), syncToDevConsole: false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error loading initial history: {ex}");
            }
        }

        public static void Add(string command, bool syncToDevConsole = true)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            string trimmed = command.Trim();
            string normalized = trimmed.StartsWith("/") ? trimmed.Substring(1).TrimStart() : trimmed;

            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            // Do not add identical consecutive command
            if (history.Count > 0 && string.Equals(history[history.Count - 1], normalized, StringComparison.OrdinalIgnoreCase))
            {
                ResetNavigation();
                return;
            }

            history.Add(normalized);

            if (history.Count > MaxHistory)
            {
                history.RemoveAt(0);
            }

            ResetNavigation();

            if (syncToDevConsole)
            {
                SyncToDevConsole(normalized);
            }
        }

        public static void Clear()
        {
            history.Clear();
            ResetNavigation();
        }

        public static void ResetNavigation()
        {
            currentIndex = history.Count;
            cachedDraft = string.Empty;
        }

        public static bool NavigateUp(string currentInput, out string result)
        {
            if (history.Count == 0)
            {
                result = currentInput ?? string.Empty;
                return false;
            }

            // If we are at the end (draft position), save the user's unsubmitted typing
            if (currentIndex >= history.Count)
            {
                cachedDraft = currentInput ?? string.Empty;
                currentIndex = history.Count;
            }

            if (currentIndex > 0)
            {
                currentIndex--;
                result = history[currentIndex];
                return true;
            }
            else
            {
                // Already at the oldest item (index 0), stay clamped there
                currentIndex = 0;
                result = history[0];
                return true;
            }
        }

        public static bool NavigateDown(string currentInput, out string result)
        {
            if (history.Count == 0)
            {
                result = currentInput ?? string.Empty;
                return false;
            }

            if (currentIndex < history.Count - 1)
            {
                currentIndex++;
                result = history[currentIndex];
                return true;
            }
            else if (currentIndex == history.Count - 1)
            {
                // Moving forward past the newest command: restore cached draft
                currentIndex = history.Count;
                result = cachedDraft;
                return true;
            }
            else
            {
                // Already at cached draft or bottom: do not erase current input
                currentIndex = history.Count;
                result = currentInput ?? string.Empty;
                return false;
            }
        }

        public static bool NavigateChatUp(InputField inputField)
        {
            if (inputField == null)
            {
                return false;
            }

            if (NavigateUp(inputField.text, out string newText))
            {
                ApplyTextToInputField(inputField, newText);
                return true;
            }

            return false;
        }

        public static bool NavigateChatDown(InputField inputField)
        {
            if (inputField == null)
            {
                return false;
            }

            if (NavigateDown(inputField.text, out string newText))
            {
                ApplyTextToInputField(inputField, newText);
                return true;
            }

            return false;
        }

        public static IEnumerator FixCaretPositionEndOfFrame(InputField inputField)
        {
            yield return new UnityEngine.WaitForEndOfFrame();
            if (inputField != null)
            {
                int len = inputField.text.Length;
                inputField.caretPosition = len;
                inputField.selectionAnchorPosition = len;
                inputField.selectionFocusPosition = len;
                inputField.MoveTextEnd(false);
            }
        }

        private static void ApplyTextToInputField(InputField inputField, string text)
        {
            text = text ?? string.Empty;
            inputField.text = text;
            inputField.caretPosition = text.Length;
            inputField.selectionAnchorPosition = text.Length;
            inputField.selectionFocusPosition = text.Length;
            inputField.MoveTextEnd(false);

            try
            {
                if (inputField.gameObject != null && inputField.gameObject.activeInHierarchy)
                {
                    inputField.StartCoroutine(FixCaretPositionEndOfFrame(inputField));
                }
            }
            catch
            {
                // In headless unit test environment StartCoroutine may not be supported
            }
        }

        public static void OnChatSubmitted(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                ResetNavigation();
                return;
            }

            string trimmed = text.Trim();
            if (IsCommand(trimmed))
            {
                Add(trimmed);
            }
            else
            {
                ResetNavigation();
            }
        }

        public static bool IsCommand(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string trimmed = text.Trim();

            // Slash / bang / dot prefixed commands (e.g. /warp, /back, /item)
            if (trimmed.StartsWith("/") || trimmed.StartsWith("!") || trimmed.StartsWith("."))
            {
                return true;
            }

            int spaceIdx = trimmed.IndexOfAny(new char[] { ' ', '\t' });
            string firstWord = (spaceIdx >= 0 ? trimmed.Substring(0, spaceIdx) : trimmed).ToLowerInvariant();

            // "back" command takes NO arguments when unslashed; casual chat like "back in 5 minutes" is NOT a command!
            if (firstWord == "back")
            {
                return spaceIdx < 0;
            }

            // "warp" command
            if (firstWord == "warp")
            {
                return true;
            }

            // Check known commands
            return IsKnownCommand(firstWord);
        }

        public static bool IsKnownCommand(string commandName)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                return false;
            }

            string cmd = commandName.ToLowerInvariant();

            if (cmd == "warp" || cmd == "back")
            {
                return true;
            }

            try
            {
                if (Subnautica.Client.Synchronizations.Processors.Player.ConsoleCommandProcessor.AllowedCommands != null &&
                    Subnautica.Client.Synchronizations.Processors.Player.ConsoleCommandProcessor.AllowedCommands.Contains(cmd))
                {
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                var dictField = AccessTools.Field(typeof(global::DevConsole), "commands");
                if (dictField != null)
                {
                    var dict = dictField.GetValue(null) as IDictionary;
                    if (dict != null && dict.Contains(cmd))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static void SyncToDevConsole(string command)
        {
            try
            {
                InitReflection();

                var devInstance = devConsoleInstanceField?.GetValue(null) as global::DevConsole;
                if (devInstance == null)
                {
                    return;
                }

                var devHist = devConsoleHistoryField?.GetValue(devInstance) as List<string>;
                if (devHist != null)
                {
                    if (devHist.Count == 0 || !string.Equals(devHist[devHist.Count - 1], command, StringComparison.OrdinalIgnoreCase))
                    {
                        devHist.Add(command);
                    }
                }

                var inputField = devConsoleInputField?.GetValue(devInstance);
                if (inputField != null && devHist != null)
                {
                    consoleInputHistoryIndexField?.SetValue(inputField, devHist.Count);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error syncing to DevConsole history: {ex}");
            }
        }

        public static void SyncWithDevConsoleOnAwake(global::DevConsole devInstance)
        {
            if (devInstance == null)
            {
                return;
            }

            try
            {
                InitReflection();

                var devHist = devConsoleHistoryField?.GetValue(devInstance) as List<string>;
                if (devHist != null)
                {
                    // Ingest any history DevConsole loaded
                    foreach (var cmd in devHist)
                    {
                        if (!string.IsNullOrWhiteSpace(cmd))
                        {
                            Add(cmd.Trim(), syncToDevConsole: false);
                        }
                    }

                    // Ensure all commands in CommandHistory are populated into DevConsole
                    foreach (var cmd in history)
                    {
                        if (!devHist.Any(h => string.Equals(h, cmd, StringComparison.OrdinalIgnoreCase)))
                        {
                            devHist.Add(cmd);
                        }
                    }

                    // Set DevConsole's inputField historyIndex to end
                    var inputField = devConsoleInputField?.GetValue(devInstance);
                    if (inputField != null)
                    {
                        consoleInputHistoryIndexField?.SetValue(inputField, devHist.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error syncing DevConsole on Awake: {ex}");
            }
        }

        public static void NormalizeDevConsoleLastHistoryEntry()
        {
            try
            {
                InitReflection();
                var devInstance = devConsoleInstanceField?.GetValue(null) as global::DevConsole;
                if (devInstance == null)
                {
                    return;
                }

                var devHist = devConsoleHistoryField?.GetValue(devInstance) as List<string>;
                if (devHist != null && devHist.Count > 0)
                {
                    string last = devHist[devHist.Count - 1];
                    if (!string.IsNullOrEmpty(last))
                    {
                        string trimmed = last.Trim();
                        string normalized = trimmed.StartsWith("/") ? trimmed.Substring(1).TrimStart() : trimmed;
                        devHist[devHist.Count - 1] = normalized;

                        // Deduplicate if previous was identical
                        if (devHist.Count > 1 && string.Equals(devHist[devHist.Count - 2], normalized, StringComparison.OrdinalIgnoreCase))
                        {
                            devHist.RemoveAt(devHist.Count - 1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error normalizing DevConsole history: {ex}");
            }
        }
    }
}

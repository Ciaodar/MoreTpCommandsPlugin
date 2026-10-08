namespace Subnautica.MoreTpCommands.Core
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Subnautica.API.Features;
    using Subnautica.Client.Synchronizations.Processors.Player;
    using UnityEngine;

    public static class CommandManager
    {
        public static void Initialize()
        {
            RegisterCommands();
        }

        public static void RegisterCommands()
        {
            try
            {
                // 1. Whitelist in multiplayer AllowedCommands
                if (!ConsoleCommandProcessor.AllowedCommands.Contains("back"))
                {
                    ConsoleCommandProcessor.AllowedCommands.Add("back");
                }
                if (!ConsoleCommandProcessor.AllowedCommands.Contains("warp"))
                {
                    ConsoleCommandProcessor.AllowedCommands.Add("warp");
                }

                // 2. Register via DevConsole.RegisterConsoleCommand
                DevConsole.RegisterConsoleCommand("back", OnDevConsoleBackCommand);
                DevConsole.RegisterConsoleCommand("warp", OnDevConsoleWarpCommand);

                Log.Info("[MoreTpCommands] Commands 'back' and 'warp' registered successfully.");
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error registering commands: {ex}");
            }
        }

        private static void OnDevConsoleBackCommand(NotificationCenter.Notification n)
        {
            HandleBackCommand();
        }

        private static void OnDevConsoleWarpCommand(NotificationCenter.Notification n)
        {
            // Fallback for native notification callback if invoked directly
        }

        public static bool HandleSubmitCommand(string fullCommand)
        {
            if (string.IsNullOrWhiteSpace(fullCommand))
            {
                return false;
            }

            string normalized = fullCommand.Trim();
            if (normalized.StartsWith("/"))
            {
                normalized = normalized.Substring(1).TrimStart();
            }

            var tokens = ParseArguments(normalized);
            if (tokens.Count == 0)
            {
                return false;
            }

            string command = tokens[0].ToLowerInvariant();
            var args = tokens.Skip(1).ToList();

            if (command == "back")
            {
                CommandHistory.Add(normalized);
                HandleBackCommand();
                return true;
            }

            if (command == "warp")
            {
                bool handled = HandleWarpCommand(args);
                if (handled)
                {
                    CommandHistory.Add(normalized);
                }
                return handled;
            }

            return false;
        }

        public static bool HandleWarpCommand(List<string> args)
        {
            // Case 1: 3 coordinate arguments (x, y, z) -> Leave for vanilla Subnautica warp!
            if (args.Count == 3 &&
                float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
                float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
                float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                return false; // Not custom handled -> let vanilla warp execute
            }

            // Case 2: No arguments -> Show Help
            if (args.Count == 0)
            {
                ShowWarpHelp();
                return true;
            }

            // Check if user asked for help
            if (args.Count == 1 && (args[0].Equals("help", StringComparison.OrdinalIgnoreCase) || args[0].Equals("?", StringComparison.OrdinalIgnoreCase)))
            {
                ShowWarpHelp();
                return true;
            }

            // Case 3: Single target player name (or arguments combined form 1 existing player name)
            if (args.Count == 1)
            {
                return ExecuteWarpToTarget(args[0]);
            }

            // Case 4: 2 arguments -> warp [player] (targetplayer)
            if (args.Count == 2)
            {
                return ExecuteWarpPlayerToTarget(args[0], args[1]);
            }

            // Case 5: More than 2 arguments (player names might have spaces without quotes)
            // Check if the entire string matches a single player
            string fullArg = string.Join(" ", args);
            if (TeleportUtility.FindPlayer(fullArg) != null)
            {
                return ExecuteWarpToTarget(fullArg);
            }

            // Try all 2-part splits
            for (int i = 1; i < args.Count; i++)
            {
                string firstPart = string.Join(" ", args.Take(i));
                string secondPart = string.Join(" ", args.Skip(i));

                if (TeleportUtility.FindPlayer(firstPart) != null && TeleportUtility.FindPlayer(secondPart) != null)
                {
                    return ExecuteWarpPlayerToTarget(firstPart, secondPart);
                }
            }

            // Fallback: If 3 arguments or invalid format, print help
            ShowWarpHelp();
            return true;
        }

        private static bool ExecuteWarpToTarget(string targetName)
        {
            var target = TeleportUtility.FindPlayer(targetName);
            if (target == null)
            {
                ErrorMessage.AddMessage($"<color=#FF6347>[MoreTpCommands]</color> Player '<color=#FFD700>{targetName}</color>' not found. Online: {TeleportUtility.GetActivePlayerNames()}");
                return true;
            }

            if (target.IsMine || target == ZeroPlayer.CurrentPlayer)
            {
                ErrorMessage.AddMessage("<color=#FFD700>[MoreTpCommands]</color> You are already at your current location!");
                return true;
            }

            Vector3 targetPos = TeleportUtility.GetPlayerPosition(target);
            if (targetPos == Vector3.zero)
            {
                ErrorMessage.AddMessage($"<color=#FF6347>[MoreTpCommands]</color> Could not retrieve valid position for <color=#FFD700>{target.NickName}</color>.");
                return true;
            }

            TeleportUtility.SafeTeleport(targetPos, target);
            ErrorMessage.AddMessage($"<color=#00FFFF>[MoreTpCommands]</color> Warped to <color=#32CD32>{target.NickName}</color>!");
            return true;
        }

        private static bool ExecuteWarpPlayerToTarget(string sourceName, string destinationName)
        {
            var sourcePlayer = TeleportUtility.FindPlayer(sourceName);
            var destPlayer = TeleportUtility.FindPlayer(destinationName);

            if (sourcePlayer == null)
            {
                ErrorMessage.AddMessage($"<color=#FF6347>[MoreTpCommands]</color> Source player '<color=#FFD700>{sourceName}</color>' not found. Online: {TeleportUtility.GetActivePlayerNames()}");
                return true;
            }

            if (destPlayer == null)
            {
                ErrorMessage.AddMessage($"<color=#FF6347>[MoreTpCommands]</color> Target player '<color=#FFD700>{destinationName}</color>' not found. Online: {TeleportUtility.GetActivePlayerNames()}");
                return true;
            }

            if (sourcePlayer == destPlayer)
            {
                ErrorMessage.AddMessage("<color=#FFD700>[MoreTpCommands]</color> Source player and target player cannot be the same!");
                return true;
            }

            Vector3 destPos = TeleportUtility.GetPlayerPosition(destPlayer);
            if (destPos == Vector3.zero)
            {
                ErrorMessage.AddMessage($"<color=#FF6347>[MoreTpCommands]</color> Could not retrieve position for <color=#FFD700>{destPlayer.NickName}</color>.");
                return true;
            }

            if (sourcePlayer.IsMine || sourcePlayer == ZeroPlayer.CurrentPlayer)
            {
                // Local player is being warped to dest
                TeleportUtility.SafeTeleport(destPos, destPlayer);
                ErrorMessage.AddMessage($"<color=#00FFFF>[MoreTpCommands]</color> Warped to <color=#32CD32>{destPlayer.NickName}</color>!");
            }
            else
            {
                // Remote player is being warped to dest
                NetworkManager.SendRemoteWarpRequest(sourcePlayer.NickName, destPlayer.NickName, destPos);
                ErrorMessage.AddMessage($"<color=#00FFFF>[MoreTpCommands]</color> Teleporting <color=#FFD700>{sourcePlayer.NickName}</color> to <color=#32CD32>{destPlayer.NickName}</color>...");
            }

            return true;
        }

        public static bool HandleBackCommand()
        {
            if (!DeathTracker.HasDeathPosition)
            {
                ErrorMessage.AddMessage("<color=#FF6347>[MoreTpCommands]</color> No death location found. You haven't died in this session yet!");
                return true;
            }

            Vector3 deathPos = DeathTracker.LastDeathPosition.Value;
            bool success = TeleportUtility.SafeTeleport(deathPos);

            if (success)
            {
                ErrorMessage.AddMessage(string.Format(
                    CultureInfo.InvariantCulture,
                    "<color=#00FFFF>[MoreTpCommands]</color> Teleported to last death location: <color=#32CD32>({0:F1}, {1:F1}, {2:F1})</color>!",
                    deathPos.x, deathPos.y, deathPos.z
                ));
            }
            else
            {
                ErrorMessage.AddMessage("<color=#FF6347>[MoreTpCommands]</color> Failed to teleport to death location.");
            }

            return true;
        }

        private static void ShowWarpHelp()
        {
            ErrorMessage.AddMessage("<color=#00FFFF>=== More TP Commands ===</color>");
            ErrorMessage.AddMessage("<color=#FFD700>warp (targetplayer)</color> -> Teleport yourself to target player");
            ErrorMessage.AddMessage("<color=#FFD700>warp [player] (targetplayer)</color> -> Teleport player to target player");
            ErrorMessage.AddMessage("<color=#FFD700>warp <x> <y> <z></color> -> Teleport to coordinates (vanilla)");
            ErrorMessage.AddMessage("<color=#FFD700>back</color> -> Teleport to your last death location");
            ErrorMessage.AddMessage($"<color=#98FB98>Online Players:</color> {TeleportUtility.GetActivePlayerNames()}");
        }

        public static List<string> ParseArguments(string commandLine)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return result;
            }

            var sb = new StringBuilder();
            bool inQuotes = false;
            char quoteChar = '\0';

            for (int i = 0; i < commandLine.Length; i++)
            {
                char c = commandLine[i];

                if (!inQuotes && (c == '"' || c == '\''))
                {
                    inQuotes = true;
                    quoteChar = c;
                }
                else if (inQuotes && c == quoteChar)
                {
                    inQuotes = false;
                    quoteChar = '\0';
                }
                else if (!inQuotes && (c == ' ' || c == '\t'))
                {
                    if (sb.Length > 0)
                    {
                        result.Add(sb.ToString());
                        sb.Clear();
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            if (sb.Length > 0)
            {
                result.Add(sb.ToString());
            }

            return result;
        }
    }
}

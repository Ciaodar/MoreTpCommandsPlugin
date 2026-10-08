namespace Subnautica.MoreTpCommands.Patches
{
    using System;
    using HarmonyLib;
    using Subnautica.API.Features;
    using Subnautica.Client.Synchronizations.Processors.General;
    using Subnautica.MoreTpCommands.Core;
    using Subnautica.Network.Models.Core;
    using Subnautica.Network.Models.Server;

    [HarmonyPatch(typeof(ChatProcessor), nameof(ChatProcessor.OnDataReceived))]
    public static class ChatProcessorPatch
    {
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(NetworkPacket networkPacket)
        {
            try
            {
                var packet = networkPacket?.GetPacket<ChatArgs>();
                if (packet != null && !string.IsNullOrEmpty(packet.Message) && packet.Message.StartsWith(NetworkManager.CommandPrefix))
                {
                    NetworkManager.HandleIncomingNetworkCommand(packet.PlayerName, packet.Message);
                    return false; // Intercept packet: do NOT show command string in chat box
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error in ChatProcessorPatch: {ex}");
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(ChatProcessor), "OnSubmitMessage")]
    public static class ChatProcessorSubmitPatch
    {
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(ref string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return true;
            }

            try
            {
                string trimmed = message.Trim();
                bool isSlashPrefixed = trimmed.StartsWith("/");
                string commandBody = isSlashPrefixed ? trimmed.Substring(1).TrimStart() : trimmed;

                if (string.IsNullOrEmpty(commandBody))
                {
                    return true;
                }

                int spaceIdx = commandBody.IndexOfAny(new char[] { ' ', '\t' });
                string cmdName = (spaceIdx >= 0 ? commandBody.Substring(0, spaceIdx) : commandBody).ToLowerInvariant();

                // If casual chat starts with "back" and has arguments without a leading slash (e.g. "back in 5 minutes"), do NOT intercept as command!
                if (!isSlashPrefixed && cmdName == "back" && spaceIdx >= 0)
                {
                    return true;
                }

                if (isSlashPrefixed || cmdName == "warp" || cmdName == "back" || CommandHistory.IsKnownCommand(cmdName))
                {
                    message = commandBody;
                    CommandHistory.Add(commandBody);

                    if (cmdName == "warp" || cmdName == "back")
                    {
                        global::DevConsole.SendConsoleCommand(commandBody);
                        return false;
                    }
                    else if (isSlashPrefixed)
                    {
                        // Route all other slash-prefixed commands to DevConsole so they aren't broadcast as chat messages
                        global::DevConsole.SendConsoleCommand(commandBody);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error in ChatProcessorSubmitPatch: {ex}");
            }

            return true;
        }
    }
}

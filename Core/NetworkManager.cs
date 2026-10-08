namespace Subnautica.MoreTpCommands.Core
{
    using System;
    using System.Globalization;
    using Subnautica.API.Features;
    using Subnautica.Client.Synchronizations.Processors.General;
    using UnityEngine;

    public static class NetworkManager
    {
        public const string CommandPrefix = "#TPCMD#";
        public const string WarpAction = "WARP";

        public static void SendRemoteWarpRequest(string targetPlayerName, string destinationPlayerName, Vector3 destinationPos)
        {
            try
            {
                string payload = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}{1}:{2}:{3}:{4:F2}:{5:F2}:{6:F2}",
                    CommandPrefix,
                    WarpAction,
                    targetPlayerName,
                    destinationPlayerName,
                    destinationPos.x,
                    destinationPos.y,
                    destinationPos.z
                );

                Log.Info($"[MoreTpCommands] Broadcasting network warp: {payload}");
                ChatProcessor.SendDataToServer(payload);
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Failed to send remote warp request: {ex}");
            }
        }

        public static bool HandleIncomingNetworkCommand(string senderName, string rawMessage)
        {
            if (string.IsNullOrEmpty(rawMessage) || !rawMessage.StartsWith(CommandPrefix))
            {
                return false;
            }

            try
            {
                string commandBody = rawMessage.Substring(CommandPrefix.Length);
                string[] parts = commandBody.Split(':');

                if (parts.Length >= 5 && parts[0] == WarpAction)
                {
                    string targetPlayerName = parts[1];
                    string destinationName = parts[2];

                    if (float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                        float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                    {
                        var currentLocal = ZeroPlayer.CurrentPlayer;
                        string myName = currentLocal?.NickName ?? Tools.GetLoggedInName();

                        bool isMe = string.Equals(myName, targetPlayerName, StringComparison.OrdinalIgnoreCase) ||
                                    (currentLocal != null && TeleportUtility.FindPlayer(targetPlayerName) == currentLocal);

                        if (isMe)
                        {
                            Vector3 targetPos = new Vector3(x, y, z);
                            var destPlayer = TeleportUtility.FindPlayer(destinationName);
                            Log.Info($"[MoreTpCommands] Received remote warp command from {senderName} to {destinationName} at {targetPos}");

                            TeleportUtility.SafeTeleport(targetPos, destPlayer);
                            ErrorMessage.AddMessage($"<color=#00FFFF>[MoreTpCommands]</color> <color=#FFD700>{senderName}</color> warped you to <color=#32CD32>{destinationName}</color>!");
                        }

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error handling incoming network command: {ex}");
            }

            return false;
        }
    }
}

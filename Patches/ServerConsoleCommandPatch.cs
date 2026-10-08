namespace Subnautica.MoreTpCommands.Patches
{
    using HarmonyLib;
    using Subnautica.Server.Processors.Player;

    [HarmonyPatch(typeof(ConsoleCommandProcessor), "IsCommandAllowed")]
    public static class ServerConsoleCommandPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(string command, ref bool __result)
        {
            if (command == "back" || command == "warp")
            {
                __result = true;
                return false;
            }

            return true;
        }
    }
}

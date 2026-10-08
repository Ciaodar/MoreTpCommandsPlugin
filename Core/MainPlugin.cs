namespace Subnautica.MoreTpCommands.Core
{
    using System;
    using HarmonyLib;
    using Subnautica.API.Enums;
    using Subnautica.API.Features;
    using Subnautica.Events.EventArgs;
    using Handlers = Subnautica.Events.Handlers;

    public class MainPlugin : SubnauticaPlugin
    {
        public override string Name { get; } = "Subnautica More TP Commands by BOT Benson & mete";

        public override SubnauticaPluginPriority Priority { get; } = SubnauticaPluginPriority.First;

        public override SubnauticaPluginType PluginType { get; } = SubnauticaPluginType.ServerClient;

        private Harmony HarmonyInstance { get; set; }

        public override void OnEnabled()
        {
            base.OnEnabled();

            Log.Info("[MoreTpCommands] Enabling More TP Commands Plugin...");

            try
            {
                DeathTracker.Initialize();
                CommandHistory.Initialize();
                CommandManager.Initialize();

                this.HarmonyInstance = new Harmony("Subnautica.Plugins.MoreTpCommands");
                this.HarmonyInstance.PatchAll();
                Log.Info("[MoreTpCommands] Harmony patches applied successfully.");

                Handlers.Player.Dead += this.OnPlayerDead;
                Handlers.Game.SceneLoaded += this.OnSceneLoaded;
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] OnEnabled failed: {ex}");
            }
        }

        public override void OnDisabled()
        {
            base.OnDisabled();

            try
            {
                if (this.HarmonyInstance != null)
                {
                    this.HarmonyInstance.UnpatchAll("Subnautica.Plugins.MoreTpCommands");
                }

                Handlers.Player.Dead -= this.OnPlayerDead;
                Handlers.Game.SceneLoaded -= this.OnSceneLoaded;
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] OnDisabled failed: {ex}");
            }
        }

        private void OnPlayerDead(PlayerDeadEventArgs ev)
        {
            try
            {
                if (global::Player.main != null)
                {
                    DeathTracker.RecordDeath(global::Player.main.transform.position);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] OnPlayerDead exception: {ex}");
            }
        }

        private void OnSceneLoaded(SceneLoadedEventArgs ev)
        {
            try
            {
                CommandManager.RegisterCommands();
                CommandHistory.LoadInitialHistory();
                CommandHistory.ResetNavigation();
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] OnSceneLoaded exception: {ex}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using Subnautica.MoreTpCommands.Core;
using UnityEngine;
using Xunit;

namespace MoreTpCommandsPlugin.Tests
{
    public class CommandTests
    {
        private static readonly Assembly FmodUnityStubAssembly = CreateFmodUnityStubAssembly();

        static CommandTests()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                if (args.Name != null && args.Name.StartsWith("FMODUnity", StringComparison.OrdinalIgnoreCase))
                {
                    return FmodUnityStubAssembly;
                }
                return null;
            };
        }

        private static Assembly CreateFmodUnityStubAssembly()
        {
            var asmName = new AssemblyName("FMODUnity");
            var asmBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            var modBuilder = asmBuilder.DefineDynamicModule("FMODUnity.dll");

            string[] classNames = new[]
            {
                "FMODAsset",
                "FMOD_StudioEventEmitter",
                "FMOD_CustomEmitter",
                "FMOD_CustomLoopingEmitter",
                "FMODUnity.StudioEventEmitter",
                "FMODUnity.EventRefAttribute"
            };
            foreach (var name in classNames)
            {
                modBuilder.DefineType(name, TypeAttributes.Public).CreateType();
            }

            string[] structNames = new[]
            {
                "FMOD.Studio.EventInstance",
                "FMOD.Studio.PARAMETER_ID",
                "FMOD.GUID",
                "FMOD.Studio.Bus"
            };
            foreach (var name in structNames)
            {
                modBuilder.DefineType(
                    name,
                    TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.Sealed,
                    typeof(ValueType)).CreateType();
            }

            return asmBuilder;
        }

        [Fact]
        public void ParseArguments_EmptyOrWhitespace_ReturnsEmptyList()
        {
            Assert.Empty(CommandManager.ParseArguments(null));
            Assert.Empty(CommandManager.ParseArguments(""));
            Assert.Empty(CommandManager.ParseArguments("    \t   "));
        }

        [Fact]
        public void ParseArguments_SingleWord_ReturnsSingleToken()
        {
            var tokens = CommandManager.ParseArguments("warp");
            Assert.Single(tokens);
            Assert.Equal("warp", tokens[0]);
        }

        [Fact]
        public void ParseArguments_MultipleWords_ReturnsTokens()
        {
            var tokens = CommandManager.ParseArguments("warp PlayerA PlayerB");
            Assert.Equal(3, tokens.Count);
            Assert.Equal("warp", tokens[0]);
            Assert.Equal("PlayerA", tokens[1]);
            Assert.Equal("PlayerB", tokens[2]);
        }

        [Fact]
        public void ParseArguments_DoubleQuotes_PreservesSpaces()
        {
            var tokens = CommandManager.ParseArguments("warp \"BOT Benson\" \"Player Two\"");
            Assert.Equal(3, tokens.Count);
            Assert.Equal("warp", tokens[0]);
            Assert.Equal("BOT Benson", tokens[1]);
            Assert.Equal("Player Two", tokens[2]);
        }

        [Fact]
        public void ParseArguments_SingleQuotes_PreservesSpaces()
        {
            var tokens = CommandManager.ParseArguments("warp 'Girl Friend' 'Mete Han'");
            Assert.Equal(3, tokens.Count);
            Assert.Equal("warp", tokens[0]);
            Assert.Equal("Girl Friend", tokens[1]);
            Assert.Equal("Mete Han", tokens[2]);
        }

        [Fact]
        public void ParseArguments_TabsAndMultipleSpaces_TrimmedCorrectly()
        {
            var tokens = CommandManager.ParseArguments("   warp \t  Player1    Player2   ");
            Assert.Equal(3, tokens.Count);
            Assert.Equal("warp", tokens[0]);
            Assert.Equal("Player1", tokens[1]);
            Assert.Equal("Player2", tokens[2]);
        }

        [Fact]
        public void ParseArguments_UnclosedQuote_ParsesRemainingTextGracefully()
        {
            var tokens = CommandManager.ParseArguments("warp \"Unclosed quote string");
            Assert.Equal(2, tokens.Count);
            Assert.Equal("warp", tokens[0]);
            Assert.Equal("Unclosed quote string", tokens[1]);
        }

        [Fact]
        public void HandleWarpCommand_Coordinates_ReturnsFalseForVanillaFallthrough()
        {
            // 3 valid floats should NOT be intercepted -> passed to vanilla warp
            var args1 = new List<string> { "100", "200", "-50" };
            Assert.False(CommandManager.HandleWarpCommand(args1));

            var args2 = new List<string> { "0.5", "-10.2", "300.99" };
            Assert.False(CommandManager.HandleWarpCommand(args2));

            // Negative floats
            var args3 = new List<string> { "-100.5", "-20.3", "-300.9" };
            Assert.False(CommandManager.HandleWarpCommand(args3));
        }

        [Fact]
        public void HandleWarpCommand_PartialCoordinates_DoesNotFallthrough()
        {
            // 2 numbers is not 3 coordinate floats -> treated as player warp
            var args = new List<string> { "100", "200" };
            Assert.True(CommandManager.HandleWarpCommand(args));

            // 3 tokens but one is non-numeric -> treated as player warp
            var argsNonNum = new List<string> { "100", "abc", "300" };
            Assert.True(CommandManager.HandleWarpCommand(argsNonNum));
        }

        [Fact]
        public void HandleWarpCommand_HelpCommand_ReturnsTrueHandled()
        {
            Assert.True(CommandManager.HandleWarpCommand(new List<string> { "help" }));
            Assert.True(CommandManager.HandleWarpCommand(new List<string> { "?" }));
            Assert.True(CommandManager.HandleWarpCommand(new List<string>()));
        }

        [Fact]
        public void HandleSubmitCommand_Back_HandledCorrectly()
        {
            Assert.True(CommandManager.HandleSubmitCommand("back"));
            Assert.True(CommandManager.HandleSubmitCommand("BACK"));
            Assert.True(CommandManager.HandleSubmitCommand("   back   "));
        }

        [Fact]
        public void HandleSubmitCommand_WarpHelp_HandledCorrectly()
        {
            Assert.True(CommandManager.HandleSubmitCommand("warp"));
            Assert.True(CommandManager.HandleSubmitCommand("WARP"));
            Assert.True(CommandManager.HandleSubmitCommand("warp help"));
        }

        [Fact]
        public void NetworkManager_PayloadFormat_MatchesSpecification()
        {
            string target = "Girlfriend";
            string dest = "Mete";
            Vector3 pos = new Vector3(105.5f, -25.25f, 300.75f);

            string payload = string.Format(
                CultureInfo.InvariantCulture,
                "{0}{1}:{2}:{3}:{4:F2}:{5:F2}:{6:F2}",
                NetworkManager.CommandPrefix,
                NetworkManager.WarpAction,
                target,
                dest,
                pos.x,
                pos.y,
                pos.z
            );

            Assert.StartsWith("#TPCMD#WARP:", payload);
            Assert.Contains("Girlfriend:Mete", payload);
            Assert.Contains("105.50:-25.25:300.75", payload);
        }

        [Fact]
        public void NetworkManager_HandleIncoming_RejectsInvalidPayloads()
        {
            Assert.False(NetworkManager.HandleIncomingNetworkCommand("Sender", null));
            Assert.False(NetworkManager.HandleIncomingNetworkCommand("Sender", ""));
            Assert.False(NetworkManager.HandleIncomingNetworkCommand("Sender", "Regular chat message"));
            Assert.False(NetworkManager.HandleIncomingNetworkCommand("Sender", "#TPCMD#INVALID:target:dest:0:0:0"));
            Assert.False(NetworkManager.HandleIncomingNetworkCommand("Sender", "#TPCMD#WARP:only_two_parts"));
            Assert.False(NetworkManager.HandleIncomingNetworkCommand("Sender", "#TPCMD#WARP:target:dest:not_a_number:0:0"));
        }

        [Fact]
        public void DeathTracker_RecordDeath_UpdatesStateCorrectly()
        {
            Vector3 death1 = new Vector3(10f, 20f, 30f);
            DeathTracker.RecordDeath(death1);

            Assert.True(DeathTracker.HasDeathPosition);
            Assert.Equal(death1, DeathTracker.LastDeathPosition.Value);

            // Record second death: must update to latest
            Vector3 death2 = new Vector3(-50f, -100f, 250f);
            DeathTracker.RecordDeath(death2);

            Assert.True(DeathTracker.HasDeathPosition);
            Assert.Equal(death2, DeathTracker.LastDeathPosition.Value);

            DeathTracker.Clear();
        }

        [Fact]
        public void LoaderCompatibility_MainPlugin_InheritsFromSubnauticaPlugin()
        {
            var type = typeof(MainPlugin);
            Assert.True(type.IsPublic);
            Assert.NotNull(type.BaseType);
            Assert.Contains("SubnauticaPlugin", type.BaseType.Name);
        }

        [Fact]
        public void CommandHistory_Add_EmptyOrWhitespace_Ignored()
        {
            CommandHistory.Clear();
            CommandHistory.Add(null, syncToDevConsole: false);
            CommandHistory.Add("", syncToDevConsole: false);
            CommandHistory.Add("   \t  ", syncToDevConsole: false);

            Assert.Empty(CommandHistory.History);
        }

        [Fact]
        public void CommandHistory_Add_TrimsAndNormalizesSlash()
        {
            CommandHistory.Clear();
            CommandHistory.Add("  warp Mete  ", syncToDevConsole: false);
            CommandHistory.Add("  /back  ", syncToDevConsole: false);

            Assert.Equal(2, CommandHistory.History.Count);
            Assert.Equal("warp Mete", CommandHistory.History[0]);
            Assert.Equal("back", CommandHistory.History[1]);
        }

        [Fact]
        public void CommandHistory_Add_ConsecutiveDuplicatesIgnored()
        {
            CommandHistory.Clear();
            CommandHistory.Add("back", syncToDevConsole: false);
            CommandHistory.Add("back", syncToDevConsole: false);
            CommandHistory.Add("BACK", syncToDevConsole: false);
            CommandHistory.Add("/back", syncToDevConsole: false);

            Assert.Single(CommandHistory.History);
            Assert.Equal("back", CommandHistory.History[0]);

            // Non-consecutive duplicate should be added
            CommandHistory.Add("warp Mete", syncToDevConsole: false);
            CommandHistory.Add("back", syncToDevConsole: false);
            Assert.Equal(3, CommandHistory.History.Count);
            Assert.Equal("back", CommandHistory.History[0]);
            Assert.Equal("warp Mete", CommandHistory.History[1]);
            Assert.Equal("back", CommandHistory.History[2]);
        }

        [Fact]
        public void CommandHistory_Add_CapacityLimitMaintained()
        {
            CommandHistory.Clear();
            int oldMax = CommandHistory.MaxHistory;
            try
            {
                CommandHistory.MaxHistory = 3;
                CommandHistory.Add("cmd1", syncToDevConsole: false);
                CommandHistory.Add("cmd2", syncToDevConsole: false);
                CommandHistory.Add("cmd3", syncToDevConsole: false);
                CommandHistory.Add("cmd4", syncToDevConsole: false);

                Assert.Equal(3, CommandHistory.History.Count);
                Assert.Equal("cmd2", CommandHistory.History[0]);
                Assert.Equal("cmd3", CommandHistory.History[1]);
                Assert.Equal("cmd4", CommandHistory.History[2]);
            }
            finally
            {
                CommandHistory.MaxHistory = oldMax;
                CommandHistory.Clear();
            }
        }

        [Fact]
        public void CommandHistory_Navigate_EmptyHistory_ReturnsFalse()
        {
            CommandHistory.Clear();
            bool up = CommandHistory.NavigateUp("hello", out string resUp);
            Assert.False(up);
            Assert.Equal("hello", resUp);

            bool down = CommandHistory.NavigateDown("hello", out string resDown);
            Assert.False(down);
            Assert.Equal("hello", resDown);
        }

        [Fact]
        public void CommandHistory_Navigate_SingleItem_WorksCorrectly()
        {
            CommandHistory.Clear();
            CommandHistory.Add("warp Mete", syncToDevConsole: false);

            // Up recalls the single item
            bool up1 = CommandHistory.NavigateUp("draft", out string text1);
            Assert.True(up1);
            Assert.Equal("warp Mete", text1);

            // Up again clamps to the single item
            bool up2 = CommandHistory.NavigateUp(text1, out string text2);
            Assert.True(up2);
            Assert.Equal("warp Mete", text2);

            // Down restores the initial cached draft
            bool down1 = CommandHistory.NavigateDown(text2, out string text3);
            Assert.True(down1);
            Assert.Equal("draft", text3);

            // Down again stays at cached draft (returns false to avoid overwriting)
            bool down2 = CommandHistory.NavigateDown(text3, out string text4);
            Assert.False(down2);
            Assert.Equal("draft", text4);
        }

        [Fact]
        public void CommandHistory_Navigate_MultipleItems_FullCycle()
        {
            CommandHistory.Clear();
            CommandHistory.Add("item titanium 5", syncToDevConsole: false);
            CommandHistory.Add("warp Mete", syncToDevConsole: false);
            CommandHistory.Add("back", syncToDevConsole: false);

            // Start with partial typing
            string current = "wa";

            // Up 1 -> "back" (most recent)
            Assert.True(CommandHistory.NavigateUp(current, out current));
            Assert.Equal("back", current);

            // Up 2 -> "warp Mete"
            Assert.True(CommandHistory.NavigateUp(current, out current));
            Assert.Equal("warp Mete", current);

            // Up 3 -> "item titanium 5" (oldest)
            Assert.True(CommandHistory.NavigateUp(current, out current));
            Assert.Equal("item titanium 5", current);

            // Up 4 -> Clamped at oldest
            Assert.True(CommandHistory.NavigateUp(current, out current));
            Assert.Equal("item titanium 5", current);

            // Down 1 -> "warp Mete"
            Assert.True(CommandHistory.NavigateDown(current, out current));
            Assert.Equal("warp Mete", current);

            // Down 2 -> "back"
            Assert.True(CommandHistory.NavigateDown(current, out current));
            Assert.Equal("back", current);

            // Down 3 -> Restores "wa"
            Assert.True(CommandHistory.NavigateDown(current, out current));
            Assert.Equal("wa", current);

            // Down 4 -> Clamped at "wa", returns false to avoid erasing typing
            Assert.False(CommandHistory.NavigateDown(current, out current));
            Assert.Equal("wa", current);
        }

        [Fact]
        public void CommandHistory_IsCommand_DetectsCommandsCorrectly()
        {
            Assert.True(CommandHistory.IsCommand("warp"));
            Assert.True(CommandHistory.IsCommand("WARP"));
            Assert.True(CommandHistory.IsCommand("warp Mete"));
            Assert.True(CommandHistory.IsCommand("back"));
            Assert.True(CommandHistory.IsCommand("BACK"));
            Assert.True(CommandHistory.IsCommand("/warp Mete"));
            Assert.True(CommandHistory.IsCommand("/back"));
            Assert.True(CommandHistory.IsCommand("/back in 5 minutes"));
            Assert.True(CommandHistory.IsCommand("/anything"));
            Assert.True(CommandHistory.IsCommand("!anything"));
            Assert.True(CommandHistory.IsCommand(".anything"));

            // Casual chat with "back" and arguments must NOT be detected as command
            Assert.False(CommandHistory.IsCommand("back in 5 minutes"));
            Assert.False(CommandHistory.IsCommand("back soon"));
            Assert.False(CommandHistory.IsCommand(null));
            Assert.False(CommandHistory.IsCommand(""));
            Assert.False(CommandHistory.IsCommand("   "));
            Assert.False(CommandHistory.IsCommand("merhaba nasilsin"));
        }

        [Fact]
        public void HandleSubmitCommand_SlashPrefixedCommands_ExecutedAndRecorded()
        {
            CommandHistory.Clear();
            DeathTracker.Clear();

            // /back should be handled and recorded
            Assert.True(CommandManager.HandleSubmitCommand("/back"));
            Assert.Contains("back", CommandHistory.History);

            // /warp help should be handled and recorded
            Assert.True(CommandManager.HandleSubmitCommand("/warp help"));
            Assert.Contains("warp help", CommandHistory.History);
        }

        [Fact]
        public void CommandHistory_NavigateDown_BeforeNavigateUp_PreservesCurrentInput()
        {
            CommandHistory.Clear();
            CommandHistory.Add("cmd1", syncToDevConsole: false);
            CommandHistory.Add("cmd2", syncToDevConsole: false);

            // Starting navigation directly with Down should NOT erase unsubmitted typing
            bool down = CommandHistory.NavigateDown("currentDraft", out string result);
            Assert.False(down);
            Assert.Equal("currentDraft", result);
        }

        [Fact]
        public void CommandHistory_Add_CaseInsensitiveConsecutiveDuplicatesIgnored()
        {
            CommandHistory.Clear();
            CommandHistory.Add("warp mete", syncToDevConsole: false);
            CommandHistory.Add("WARP METE", syncToDevConsole: false);
            CommandHistory.Add("Warp Mete", syncToDevConsole: false);

            Assert.Single(CommandHistory.History);
            Assert.Equal("warp mete", CommandHistory.History[0]);
        }

        [Fact]
        public void CommandHistory_Add_PreservesQuotesAndArguments()
        {
            CommandHistory.Clear();
            string complexCmd = "warp \"BOT Benson\" 'Player Two'";
            CommandHistory.Add(complexCmd, syncToDevConsole: false);

            Assert.Single(CommandHistory.History);
            Assert.Equal(complexCmd, CommandHistory.History[0]);
        }

        [Fact]
        public void CommandHistory_CapacityLimit_OneItem_RetainsOnlyLatest()
        {
            CommandHistory.Clear();
            int oldMax = CommandHistory.MaxHistory;
            try
            {
                CommandHistory.MaxHistory = 1;
                CommandHistory.Add("first", syncToDevConsole: false);
                Assert.Single(CommandHistory.History);
                Assert.Equal("first", CommandHistory.History[0]);

                CommandHistory.Add("second", syncToDevConsole: false);
                Assert.Single(CommandHistory.History);
                Assert.Equal("second", CommandHistory.History[0]);
            }
            finally
            {
                CommandHistory.MaxHistory = oldMax;
                CommandHistory.Clear();
            }
        }

        [Fact]
        public void CommandHistory_Navigate_NullInputsHandledGracefully()
        {
            CommandHistory.Clear();
            CommandHistory.Add("warp Mete", syncToDevConsole: false);

            // Null input text passed to NavigateUp
            bool up = CommandHistory.NavigateUp(null, out string result);
            Assert.True(up);
            Assert.Equal("warp Mete", result);

            // Null input text passed to NavigateDown
            bool down = CommandHistory.NavigateDown(null, out string resultDown);
            Assert.True(down);
            Assert.Equal("", resultDown); // Restored draft was null -> converted to string.Empty
        }

        [Fact]
        public void CommandHistory_NavigateDown_AtDraft_NeverErasesUserTyping()
        {
            CommandHistory.Clear();
            CommandHistory.Add("cmd1", syncToDevConsole: false);
            CommandHistory.Add("cmd2", syncToDevConsole: false);

            string userTyping = "partially typed message";

            // User has typed something and presses Down
            bool down = CommandHistory.NavigateDown(userTyping, out string result);
            Assert.False(down);
            Assert.Equal(userTyping, result);

            // Navigate up to cmd2
            Assert.True(CommandHistory.NavigateUp(userTyping, out string up1));
            Assert.Equal("cmd2", up1);

            // Navigate down to restore userTyping
            Assert.True(CommandHistory.NavigateDown(up1, out string down1));
            Assert.Equal(userTyping, down1);

            // Pressing down again while at userTyping must NOT erase it
            Assert.False(CommandHistory.NavigateDown(down1, out string down2));
            Assert.Equal(userTyping, down2);
        }

        [Fact]
        public void CommandHistory_IsCommand_BackVsCasualChat_DistinguishedProperly()
        {
            // Exact back commands
            Assert.True(CommandHistory.IsCommand("back"));
            Assert.True(CommandHistory.IsCommand("BACK"));
            Assert.True(CommandHistory.IsCommand("  back  "));
            Assert.True(CommandHistory.IsCommand("/back"));
            Assert.True(CommandHistory.IsCommand("/back in 5 minutes"));

            // Casual chat starting with back
            Assert.False(CommandHistory.IsCommand("back in 5 minutes"));
            Assert.False(CommandHistory.IsCommand("back to base"));
            Assert.False(CommandHistory.IsCommand("back soon"));
            Assert.False(CommandHistory.IsCommand("back!"));
        }

        [Fact]
        public void CommandHistory_LoadInitialHistory_ParsesLinesCorrectly()
        {
            CommandHistory.Clear();

            // Set simulated MiscSettings console history
            MiscSettings.consoleHistory = "warp PlayerA\r\nback\n/warp PlayerB\nitem titanium 5";
            try
            {
                CommandHistory.LoadInitialHistory();

                Assert.Equal(4, CommandHistory.History.Count);
                Assert.Equal("warp PlayerA", CommandHistory.History[0]);
                Assert.Equal("back", CommandHistory.History[1]);
                Assert.Equal("warp PlayerB", CommandHistory.History[2]);
                Assert.Equal("item titanium 5", CommandHistory.History[3]);
            }
            finally
            {
                MiscSettings.consoleHistory = string.Empty;
                CommandHistory.Clear();
            }
        }

        [Fact]
        public void TeleportUtility_TryParseWarpCoordinates_ValidAndInvalidInputs()
        {
            Assert.True(TeleportUtility.TryParseWarpCoordinates(new List<string> { "100", "20", "-50" }, out Vector3 coords1));
            Assert.Equal(new Vector3(100f, 20f, -50f), coords1);

            Assert.True(TeleportUtility.TryParseWarpCoordinates(new List<string> { "-125.5", "0.25", "400.75" }, out Vector3 coords2));
            Assert.Equal(new Vector3(-125.5f, 0.25f, 400.75f), coords2);

            // Reject wrong argument counts, null, non-numeric, NaN, and Infinity
            Assert.False(TeleportUtility.TryParseWarpCoordinates(null, out _));
            Assert.False(TeleportUtility.TryParseWarpCoordinates(new List<string>(), out _));
            Assert.False(TeleportUtility.TryParseWarpCoordinates(new List<string> { "10", "20" }, out _));
            Assert.False(TeleportUtility.TryParseWarpCoordinates(new List<string> { "10", "20", "30", "40" }, out _));
            Assert.False(TeleportUtility.TryParseWarpCoordinates(new List<string> { "10", "abc", "30" }, out _));
            Assert.False(TeleportUtility.TryParseWarpCoordinates(new List<string> { "NaN", "0", "0" }, out _));
            Assert.False(TeleportUtility.TryParseWarpCoordinates(new List<string> { "Infinity", "0", "0" }, out _));
            Assert.False(TeleportUtility.TryParseWarpCoordinates(new List<string> { "-Infinity", "0", "0" }, out _));
        }

        [Fact]
        public void TeleportUtility_GracePeriod_TracksAndClearsStateCorrectly()
        {
            TeleportUtility.ClearTeleportGracePeriod();
            Assert.False(TeleportUtility.IsInTeleportGracePeriod());

            Vector3 origin = new Vector3(10f, -20f, 30f);
            Vector3 target = new Vector3(200f, -5f, -150f);
            TeleportUtility.MarkTeleportTimestamp(origin, target, targetIsExterior: true);

            try
            {
                Assert.True(TeleportUtility.IsInTeleportGracePeriod());
                Assert.Equal(origin, TeleportUtility.LastPreTeleportPosition);
                Assert.Equal(target, TeleportUtility.LastTeleportTarget);
                Assert.True(TeleportUtility.LastTeleportTargetIsExterior);
            }
            finally
            {
                TeleportUtility.ClearTeleportGracePeriod();
            }

            Assert.False(TeleportUtility.IsInTeleportGracePeriod());
        }

        [Fact]
        public void TeleportUtility_ShouldBlockInteriorTrigger_PreventsSpuriousInteriorReEntry()
        {
            TeleportUtility.ClearTeleportGracePeriod();

            Vector3 baseTriggerPos = new Vector3(50f, -15f, 50f);
            Vector3 distantExteriorPos = new Vector3(300f, -5f, -200f);
            Vector3 nearbyNormalWalkPos = new Vector3(51f, -14.5f, 50f);

            // 1. Even outside grace period, OnTriggerExit when player is far from the trigger (> 6m) MUST be blocked
            Assert.True(TeleportUtility.ShouldBlockInteriorTrigger(baseTriggerPos, distantExteriorPos, isExitEvent: true));

            // 2. Outside grace period, normal walk across entrance trigger (< 6m) MUST be allowed
            Assert.False(TeleportUtility.ShouldBlockInteriorTrigger(baseTriggerPos, nearbyNormalWalkPos, isExitEvent: true));
            Assert.False(TeleportUtility.ShouldBlockInteriorTrigger(baseTriggerPos, nearbyNormalWalkPos, isExitEvent: false));

            // 3. During teleport grace period to an exterior target, OnTriggerExit MUST be blocked even if target is near the base
            Vector3 nearbyExteriorRoofPos = new Vector3(50f, -11f, 50f);
            TeleportUtility.MarkTeleportTimestamp(baseTriggerPos, nearbyExteriorRoofPos, targetIsExterior: true);
            try
            {
                Assert.True(TeleportUtility.ShouldBlockInteriorTrigger(baseTriggerPos, nearbyExteriorRoofPos, isExitEvent: true));
                Assert.True(TeleportUtility.ShouldBlockInteriorTrigger(baseTriggerPos, distantExteriorPos, isExitEvent: false));
            }
            finally
            {
                TeleportUtility.ClearTeleportGracePeriod();
            }
        }

        [Fact]
        public void TeleportUtility_IsTargetPlayerInInterior_EvaluatesNullSafely()
        {
            Assert.False(TeleportUtility.IsTargetPlayerInInterior(null));
        }

        [Fact]
        public void TeleportUtility_ShouldBlockSeaTruckModeActivation_BlocksDuringExteriorTeleportGracePeriod()
        {
            TeleportUtility.ClearTeleportGracePeriod();
            Assert.False(TeleportUtility.ShouldBlockSeaTruckModeActivation());

            Vector3 origin = new Vector3(10f, -20f, 30f);
            Vector3 target = new Vector3(200f, -5f, -150f);
            TeleportUtility.MarkTeleportTimestamp(origin, target, targetIsExterior: true);
            try
            {
                Assert.True(TeleportUtility.ShouldBlockSeaTruckModeActivation());
                Assert.True(TeleportUtility.ShouldBlockSeaTruckModeActivation(origin, target));
            }
            finally
            {
                TeleportUtility.ClearTeleportGracePeriod();
            }

            TeleportUtility.MarkTeleportTimestamp(origin, target, targetIsExterior: false);
            try
            {
                Assert.False(TeleportUtility.ShouldBlockSeaTruckModeActivation());
                // Even if target is an interior, a distant origin SeaTruck (dist > 8m) must still be blocked
                Assert.True(TeleportUtility.ShouldBlockSeaTruckModeActivation(origin, target));
                // Whereas a SeaTruck at the target position (< 8m) is allowed
                Assert.False(TeleportUtility.ShouldBlockSeaTruckModeActivation(target, target + Vector3.up));
            }
            finally
            {
                TeleportUtility.ClearTeleportGracePeriod();
            }
        }

        [Fact]
        public void AllPluginHarmonyPatches_TargetRealExistingMethodsInGameAssemblies()
        {
            var pluginAssembly = typeof(MainPlugin).Assembly;
            var patchTypes = new List<Type>();

            foreach (var type in pluginAssembly.GetTypes())
            {
                var attrs = System.Reflection.CustomAttributeData.GetCustomAttributes(type);
                foreach (var attr in attrs)
                {
                    if (attr.AttributeType == typeof(HarmonyLib.HarmonyPatch))
                    {
                        patchTypes.Add(type);
                        break;
                    }
                }
            }

            Assert.Contains(typeof(Subnautica.MoreTpCommands.Patches.BaseEntranceTriggerExitPatch), patchTypes);
            Assert.Contains(typeof(Subnautica.MoreTpCommands.Patches.BaseEntranceTriggerStayPatch), patchTypes);
            Assert.Contains(typeof(Subnautica.MoreTpCommands.Patches.PrecursorMoonPoolTriggerUpdatePatch), patchTypes);
            Assert.Contains(typeof(Subnautica.MoreTpCommands.Patches.SeaTruckCustomMotorChangeModePatch), patchTypes);
            Assert.Contains(typeof(Subnautica.MoreTpCommands.Patches.PlayerWarpConsoleCommandPatch), patchTypes);
            Assert.Contains(typeof(Subnautica.MoreTpCommands.Patches.GotoConsoleCommandGotoPositionPatch), patchTypes);
            Assert.Contains(typeof(Subnautica.MoreTpCommands.Patches.PlayerPositionCheatPatch), patchTypes);

            foreach (var patchType in patchTypes)
            {
                System.Reflection.CustomAttributeData harmonyAttr = null;
                foreach (var attr in System.Reflection.CustomAttributeData.GetCustomAttributes(patchType))
                {
                    if (attr.AttributeType == typeof(HarmonyLib.HarmonyPatch))
                    {
                        harmonyAttr = attr;
                        break;
                    }
                }

                Assert.NotNull(harmonyAttr);
                Assert.True(harmonyAttr.ConstructorArguments.Count >= 2, $"Patch {patchType.Name} must specify target type and method name.");
                var targetType = harmonyAttr.ConstructorArguments[0].Value as Type;
                var methodName = harmonyAttr.ConstructorArguments[1].Value as string;

                Assert.NotNull(targetType);
                Assert.False(string.IsNullOrEmpty(methodName), $"Patch {patchType.Name} has empty target method name.");

                var methods = targetType.GetMethods(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Static);

                Assert.Contains(methods, m => m.Name == methodName);
            }
        }

        [Fact]
        public void TeleportUtility_ReflectedMembers_ExistInGameAssemblies()
        {
            Assert.NotNull(HarmonyLib.AccessTools.Method(typeof(global::SeaTruckSegment), "GetFirstSegment"));
            Assert.NotNull(HarmonyLib.AccessTools.Method(typeof(global::Player), "UpdateMotorMode"));
            Assert.NotNull(HarmonyLib.AccessTools.Method(typeof(global::Player), "SetPrecursorOutOfWater"));
            Assert.NotNull(HarmonyLib.AccessTools.Field(typeof(global::Player), "forceWalkMotorMode"));
            Assert.NotNull(HarmonyLib.AccessTools.Field(typeof(global::Player), "currentMountedVehicle"));
            Assert.NotNull(HarmonyLib.AccessTools.Method(typeof(global::PlayerController), "HandleControllerStateAfterDeserialization"));
            Assert.NotNull(HarmonyLib.AccessTools.Method(typeof(global::PlayerController), "HandleUnderWaterState"));
            Assert.NotNull(HarmonyLib.AccessTools.Method(typeof(global::PlayerController), "HandleControllerState"));
            Assert.NotNull(HarmonyLib.AccessTools.Method(typeof(global::PrecursorMoonPoolTrigger), "CheckPlayerWithinBounds"));
            Assert.NotNull(HarmonyLib.AccessTools.Field(typeof(global::PrecursorMoonPoolTrigger), "checkPlayer"));
            Assert.NotNull(HarmonyLib.AccessTools.Field(typeof(global::SeaTruckAnimation), "_currentAnimation"));
            Assert.NotNull(HarmonyLib.AccessTools.Field(typeof(global::GotoConsoleCommand), "movingPlayer"));
            Assert.NotNull(HarmonyLib.AccessTools.Field(typeof(global::GotoConsoleCommand), "continueSpam"));
        }
    }
}


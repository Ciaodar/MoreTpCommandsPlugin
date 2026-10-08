namespace Subnautica.MoreTpCommands.Core
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using HarmonyLib;
    using Subnautica.API.Extensions;
    using Subnautica.API.Features;
    using Subnautica.Client.MonoBehaviours.Player;
    using Subnautica.Events.EventArgs;
    using Subnautica.Events.Patches.Fixes.Vehicle;
    using UnityEngine;

    public static class TeleportUtility
    {
        public const double DefaultTeleportGraceSeconds = 0.75;
        public const float MaxValidEntranceExitDistance = 6.0f;

        private static MethodInfo seaTruckGetFirstSegmentMethod;
        private static MethodInfo playerUpdateMotorModeMethod;
        private static MethodInfo playerControllerHandleStateAfterDeserializationMethod;
        private static MethodInfo playerControllerHandleUnderWaterStateMethod;
        private static MethodInfo playerControllerHandleStateMethod;
        private static MethodInfo precursorCheckPlayerWithinBoundsMethod;
        private static FieldInfo playerCurrentMountedVehicleField;
        private static FieldInfo seaTruckAnimationCurrentAnimField;
        private static FieldInfo gotoMovingPlayerField;
        private static FieldInfo gotoContinueSpamField;
        private static FieldInfo precursorCheckPlayerField;

        private static long lastTeleportUtcTicks = 0;
        private static Vector3 lastPreTeleportPosition = Vector3.zero;
        private static Vector3 lastTeleportTarget = Vector3.zero;
        private static bool lastTeleportTargetIsExterior = true;

        public static Vector3 LastPreTeleportPosition => lastPreTeleportPosition;
        public static Vector3 LastTeleportTarget => lastTeleportTarget;
        public static bool LastTeleportTargetIsExterior => lastTeleportTargetIsExterior;

        public static void MarkTeleportTimestamp(Vector3 preTeleportPos, Vector3 targetPos, bool targetIsExterior = true)
        {
            lastPreTeleportPosition = preTeleportPos;
            lastTeleportTarget = targetPos;
            lastTeleportTargetIsExterior = targetIsExterior;
            lastTeleportUtcTicks = DateTime.UtcNow.Ticks;
        }

        public static void ClearTeleportGracePeriod()
        {
            lastTeleportUtcTicks = 0;
            lastPreTeleportPosition = Vector3.zero;
            lastTeleportTarget = Vector3.zero;
            lastTeleportTargetIsExterior = true;
        }

        public static bool IsInTeleportGracePeriod(double durationSeconds = DefaultTeleportGraceSeconds)
        {
            long ticks = lastTeleportUtcTicks;
            if (ticks <= 0)
            {
                return false;
            }

            double elapsedSeconds = (DateTime.UtcNow.Ticks - ticks) / (double)TimeSpan.TicksPerSecond;
            return elapsedSeconds >= 0.0 && elapsedSeconds <= durationSeconds;
        }

        public static bool IsGotoMovingPlayer()
        {
            try
            {
                var gotoCmd = global::GotoConsoleCommand.main;
                if (gotoCmd == null)
                {
                    return false;
                }

                var field = gotoMovingPlayerField ??= AccessTools.Field(typeof(global::GotoConsoleCommand), "movingPlayer");
                return field != null && (bool)field.GetValue(gotoCmd);
            }
            catch
            {
                return false;
            }
        }

        public static bool ShouldBlockInteriorTrigger(
            Vector3 triggerPosition,
            Vector3 playerPosition,
            bool isExitEvent,
            float maxExitDistance = MaxValidEntranceExitDistance)
        {
            float distSqToPlayer = (playerPosition - triggerPosition).sqrMagnitude;
            float maxDistSq = maxExitDistance * maxExitDistance;

            // OnTriggerExit on BaseEntranceTrigger fires when the player's collider leaves the trigger volume.
            // If the player was teleported far away from the trigger, OnTriggerExit must NEVER pull the player back into the interior.
            if (isExitEvent && distSqToPlayer > maxDistSq)
            {
                return true;
            }

            if (IsInTeleportGracePeriod() || IsGotoMovingPlayer())
            {
                // During the post-teleport grace window, always block OnTriggerExit re-entry into an interior
                // if the destination is exterior or if the trigger is not at the destination.
                if (isExitEvent && (lastTeleportTargetIsExterior || (lastTeleportTarget - triggerPosition).sqrMagnitude > maxDistSq))
                {
                    return true;
                }

                // Also block OnTriggerStay if the trigger is far from the current player position.
                if (distSqToPlayer > maxDistSq)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ShouldBlockSeaTruckModeActivation(
            Vector3? seaTruckPosition = null,
            Vector3? playerPosition = null,
            float maxDistance = 8.0f)
        {
            if (IsInTeleportGracePeriod() || IsGotoMovingPlayer())
            {
                if (lastTeleportTargetIsExterior)
                {
                    return true;
                }

                if (seaTruckPosition.HasValue && playerPosition.HasValue)
                {
                    float distSq = (playerPosition.Value - seaTruckPosition.Value).sqrMagnitude;
                    if (distSq > maxDistance * maxDistance)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool ShouldBlockSeaTruckModeActivation(global::SeaTruckSegment seaTruck, float maxDistance = 8.0f)
        {
            if (!IsInTeleportGracePeriod() && !IsGotoMovingPlayer())
            {
                return false;
            }

            if (lastTeleportTargetIsExterior)
            {
                return true;
            }

            try
            {
                if (!object.ReferenceEquals(seaTruck, null) && seaTruck != null && global::Player.main != null)
                {
                    return ShouldBlockSeaTruckModeActivation(
                        seaTruck.transform.position,
                        global::Player.main.transform.position,
                        maxDistance);
                }
            }
            catch
            {
                // Ignore if Unity native object is uninitialized
            }

            return false;
        }

        public static bool TryParseCoordinate(string value, out float result)
        {
            result = 0f;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
                !float.IsNaN(result) && !float.IsInfinity(result))
            {
                return true;
            }

            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out result) &&
                !float.IsNaN(result) && !float.IsInfinity(result))
            {
                return true;
            }

            return false;
        }

        public static bool TryParseWarpCoordinates(IList<string> args, out Vector3 coordinates)
        {
            coordinates = Vector3.zero;
            if (args == null || args.Count != 3)
            {
                return false;
            }

            if (TryParseCoordinate(args[0], out float x) &&
                TryParseCoordinate(args[1], out float y) &&
                TryParseCoordinate(args[2], out float z))
            {
                coordinates = new Vector3(x, y, z);
                return true;
            }

            return false;
        }

        public static ZeroPlayer FindPlayer(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            string cleanName = name.Trim().Trim('"', '\'');

            // Special self keywords
            if (cleanName.Equals("me", StringComparison.OrdinalIgnoreCase) ||
                cleanName.Equals("myself", StringComparison.OrdinalIgnoreCase) ||
                cleanName.Equals("self", StringComparison.OrdinalIgnoreCase))
            {
                return ZeroPlayer.CurrentPlayer;
            }

            var allPlayers = ZeroPlayer.GetAllPlayers();
            if (allPlayers == null || allPlayers.Count == 0)
            {
                if (ZeroPlayer.CurrentPlayer != null && MatchesName(ZeroPlayer.CurrentPlayer, cleanName))
                {
                    return ZeroPlayer.CurrentPlayer;
                }
                return null;
            }

            // 1. Exact match (case insensitive)
            var match = allPlayers.FirstOrDefault(p => p != null && !string.IsNullOrEmpty(p.NickName) &&
                                                      p.NickName.Equals(cleanName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match;
            }

            // 2. Starts with match (case insensitive)
            match = allPlayers.FirstOrDefault(p => p != null && !string.IsNullOrEmpty(p.NickName) &&
                                                  p.NickName.StartsWith(cleanName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match;
            }

            // 3. Contains match (case insensitive)
            match = allPlayers.FirstOrDefault(p => p != null && !string.IsNullOrEmpty(p.NickName) &&
                                                  p.NickName.IndexOf(cleanName, StringComparison.OrdinalIgnoreCase) >= 0);
            if (match != null)
            {
                return match;
            }

            // Check against logged in name
            string localName = Tools.GetLoggedInName();
            if (!string.IsNullOrEmpty(localName) && localName.IndexOf(cleanName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ZeroPlayer.CurrentPlayer;
            }

            return null;
        }

        private static bool MatchesName(ZeroPlayer player, string name)
        {
            if (player == null) return false;
            if (!string.IsNullOrEmpty(player.NickName) && player.NickName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            string localName = Tools.GetLoggedInName();
            return !string.IsNullOrEmpty(localName) && localName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static Vector3 GetPlayerPosition(ZeroPlayer player)
        {
            if (player == null)
            {
                return Vector3.zero;
            }

            if (player.IsMine)
            {
                return global::Player.main != null ? global::Player.main.transform.position : player.Position;
            }

            if (player.PlayerModel != null && player.PlayerModel.transform != null)
            {
                return player.PlayerModel.transform.position;
            }

            // If remote player is inside a SeaTruck and PlayerModel is unavailable, player.Position is local to the SeaTruck segment
            if (player.IsInSeaTruck && !string.IsNullOrEmpty(player.CurrentInteriorId))
            {
                try
                {
                    var seaTruckSegment = Network.Identifier.GetComponentByGameObject<global::SeaTruckSegment>(player.CurrentInteriorId, true);
                    if (seaTruckSegment != null && seaTruckSegment.transform != null)
                    {
                        return seaTruckSegment.transform.TransformPoint(player.Position);
                    }
                }
                catch
                {
                    // Fall through to standard position checks
                }
            }

            if (player.Position != Vector3.zero)
            {
                return player.Position;
            }

            if (player.VehiclePosition != Vector3.zero)
            {
                return player.VehiclePosition;
            }

            return player.Position;
        }

        public static bool IsTargetPlayerInInterior(ZeroPlayer targetPlayer)
        {
            if (targetPlayer == null)
            {
                return false;
            }

            return !string.IsNullOrEmpty(targetPlayer.CurrentSubRootId) ||
                   !string.IsNullOrEmpty(targetPlayer.CurrentInteriorId) ||
                   targetPlayer.IsInSeaTruck;
        }

        public static bool SafeTeleport(Vector3 targetPosition)
        {
            return SafeTeleport(targetPosition, null);
        }

        public static bool SafeTeleport(Vector3 targetPosition, ZeroPlayer targetPlayer)
        {
            try
            {
                var player = global::Player.main;
                if (player == null)
                {
                    Log.Error("[MoreTpCommands] Cannot teleport: Player.main is null!");
                    return false;
                }

                Vector3 safePos = targetPosition + (Vector3.up * 0.15f);
                bool targetIsExterior = !IsTargetPlayerInInterior(targetPlayer);

                MarkTeleportTimestamp(player.transform.position, safePos, targetIsExterior);

                // 1. Cleanly exit all vehicles, SeaTruckCustomMotor, interiors, SubRoots, and moving platforms
                PreparePlayerForTeleport(player, safePos, stopGotoCoroutines: true);

                // 2. Apply target position while CharacterController is disabled so it does not resist or revert the move
                player.SetPosition(safePos);
                Physics.SyncTransforms();

                if (player.rigidBody != null)
                {
                    player.rigidBody.velocity = Vector3.zero;
                    player.rigidBody.angularVelocity = Vector3.zero;
                }

                // 3. Restore normal mode, underwater/motor state, and re-enable controllers & colliders
                FinalizePlayerTeleportState(player, safePos, targetPlayer);

                // 4. Schedule multi-frame verification to guard against asynchronous FixedUpdate trigger callbacks
                player.StartCoroutine(VerifyPostTeleportStateCoroutine(player, safePos, targetPlayer));

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] SafeTeleport exception: {ex}");
                return false;
            }
        }

        public static void PreparePlayerForTeleport(global::Player player, Vector3 targetPosition, bool stopGotoCoroutines = true)
        {
            if (player == null)
            {
                return;
            }

            // Cancel any in-progress vanilla GotoConsoleCommand travel coroutine and reset its movingPlayer flag
            if (stopGotoCoroutines)
            {
                try
                {
                    var gotoCmd = global::GotoConsoleCommand.main;
                    if (gotoCmd != null)
                    {
                        gotoCmd.StopAllCoroutines();
                        (gotoMovingPlayerField ??= AccessTools.Field(typeof(global::GotoConsoleCommand), "movingPlayer"))?.SetValue(gotoCmd, false);
                        (gotoContinueSpamField ??= AccessTools.Field(typeof(global::GotoConsoleCommand), "continueSpam"))?.SetValue(gotoCmd, false);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[MoreTpCommands] Error stopping GotoConsoleCommand coroutines: {ex}");
                }
            }

            // 1. Handle SeaTruck piloting chair and SeaTruckAnimation cinematics
            global::SeaTruckSegment seaTruckSegment = null;
            try
            {
                seaTruckSegment = player.GetComponentInParent<global::SeaTruckSegment>() ?? (player.currentInterior as global::SeaTruckSegment);
                if (seaTruckSegment == null && player.TryGetComponent<SeaTruckCustomMotor>(out var customMotorSeg) && customMotorSeg != null)
                {
                    seaTruckSegment = customMotorSeg.SeaTruckSegment;
                }

                if (seaTruckSegment != null)
                {
                    ResetSeaTruckAnimations(seaTruckSegment);

                    if (player.inSeatruckPilotingChair && seaTruckSegment.motor != null)
                    {
                        seaTruckSegment.motor.StopPiloting(waitForDocking: false, forceStop: true, skipUnsubscribe: false, immediate: true);
                        // StopPiloting when !isMainCab starts EndPilot animation; immediately clear it so OnPlayerCinematicModeEnd doesn't re-enter SeaTruckCustomMotor
                        ResetSeaTruckAnimations(seaTruckSegment);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error exiting SeaTruck piloting chair: {ex}");
            }

            // 2. Handle Multiplayer SeaTruckCustomMotor (locks Y to 0.01f and disables PlayerController when walking inside a SeaTruck)
            try
            {
                DeactivateSeaTruckCustomMotor(player);
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error deactivating SeaTruckCustomMotor: {ex}");
            }

            // 3. Handle vanilla SeaTruckSegment interior state
            try
            {
                var segment = seaTruckSegment ?? player.GetComponentInParent<global::SeaTruckSegment>() ?? (player.currentInterior as global::SeaTruckSegment);
                if (segment != null)
                {
                    var firstSegment = GetFirstSeaTruckSegment(segment);
                    if (firstSegment != null && firstSegment != segment)
                    {
                        firstSegment.Exit(skipAnimations: true, skipTeleport: true);
                    }
                    segment.Exit(skipAnimations: true, skipTeleport: true);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error exiting SeaTruckSegment interior: {ex}");
            }

            // 4. Handle Hovercraft / Snowfox
            if (player.inHovercraft)
            {
                try
                {
                    var hoverbike = player.GetComponentInParent<global::Hoverbike>();
                    if (hoverbike != null)
                    {
                        hoverbike.ExitVehicle();
                    }
                    player.inHovercraft = false;
                }
                catch (Exception ex)
                {
                    Log.Error($"[MoreTpCommands] Error exiting hovercraft: {ex}");
                }
            }

            // 5. Handle Exosuit / mounted vehicle (findNewPosition: false ensures ExitLockedMode never calls SpawnNearby and always unparents/resets mode)
            try
            {
                var mountedVehicle = player.GetVehicle();
                if (mountedVehicle != null || player.inExosuit)
                {
                    string vehicleUniqueId = null;
                    TechType vehicleTechType = TechType.None;
                    if (mountedVehicle != null && Network.IsMultiplayerActive)
                    {
                        vehicleUniqueId = Network.Identifier.GetIdentityId(mountedVehicle.gameObject, false);
                        vehicleTechType = CraftData.GetTechType(mountedVehicle.gameObject);
                    }

                    player.ExitLockedMode(respawn: false, findNewPosition: false);

                    if (mountedVehicle != null)
                    {
                        mountedVehicle.pilotId = string.Empty;
                    }

                    var remainingVehicle = player.GetVehicle();
                    if (remainingVehicle != null)
                    {
                        remainingVehicle.pilotId = string.Empty;
                        (playerCurrentMountedVehicleField ??= AccessTools.Field(typeof(global::Player), "currentMountedVehicle"))?.SetValue(player, null);
                    }

                    player.inExosuit = false;
                    player.inHovercraft = false;
                    if (player.armsController != null)
                    {
                        player.armsController.SetWorldIKTarget(null, null);
                    }

                    // Workaround for Multiplayer Vehicle_OnPilotModeEnd patch typing __instance as Hoverbike instead of Vehicle
                    if (Network.IsMultiplayerActive && vehicleTechType == TechType.Exosuit && !string.IsNullOrEmpty(vehicleUniqueId))
                    {
                        try
                        {
                            Subnautica.Events.Handlers.Vehicle.OnExited(new VehicleExitedEventArgs(vehicleUniqueId, vehicleTechType));
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[MoreTpCommands] Error broadcasting Exosuit exit event: {ex}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error exiting mounted vehicle: {ex}");
            }

            // 6. Handle piloting, locked, or sitting mode (findNewPosition: false so SpawnNearby is never invoked)
            try
            {
                if (player.isPiloting)
                {
                    player.ExitPilotingMode();
                }
                player.ExitLockedMode(respawn: false, findNewPosition: false);
                player.ExitSittingMode();
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error resetting player modes: {ex}");
            }

            // 7. Exit Interior, SubRoot, and Precursor dry-space states cleanly (firing Multiplayer sync hooks via ExitCurrentInterior / SetCurrentSub)
            try
            {
                if (player.currentInterior != null)
                {
                    player.ExitCurrentInterior();
                }

                if (player.GetCurrentSub() != null)
                {
                    player.SetCurrentSub(null);
                }

                SetPrecursorOutOfWater(player, false);

                if (player.transform.parent != null)
                {
                    if (player.rigidBody != null)
                    {
                        player.rigidBody.SetNonKinematic();
                    }
                    player.transform.parent = null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error clearing interior/subroot state: {ex}");
            }

            // 8. Clear GroundMotor moving platform state and temporarily disable PlayerController before transform move
            try
            {
                ClearGroundMotorPlatformState(player);
                if (player.playerController != null)
                {
                    player.playerController.SetEnabled(false);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error preparing PlayerController for teleport: {ex}");
            }
        }

        public static void FinalizePlayerTeleportState(global::Player player, Vector3 safePos, ZeroPlayer targetPlayer = null)
        {
            if (player == null)
            {
                return;
            }

            try
            {
                DeactivateSeaTruckCustomMotor(player);

                player.ToNormalMode(findNewPosition: false);

                // Call OnPlayerPositionCheat() BEFORE applying any destination interior/SubRoot
                // because OnPlayerPositionCheat() resets currentSub = null, currentWaterPark = null, forceWalkMotorMode = false, and calls ExitCurrentInterior()
                if (player.GetCurrentSub() != null)
                {
                    player.SetCurrentSub(null);
                }
                player.OnPlayerPositionCheat();
                SetPrecursorOutOfWater(player, false);

                if (player.transform.parent != null)
                {
                    player.transform.parent = null;
                }

                // If teleporting to a remote player who is inside a synced SubRoot (Base) or IInteriorSpace, enter that destination interior AFTER OnPlayerPositionCheat()
                bool enteredTargetSeaTruck = false;
                if (targetPlayer != null)
                {
                    global::SubRoot targetSubRoot = null;
                    if (!string.IsNullOrEmpty(targetPlayer.CurrentSubRootId))
                    {
                        try
                        {
                            targetSubRoot = Network.Identifier.GetComponentByGameObject<global::SubRoot>(targetPlayer.CurrentSubRootId, true);
                        }
                        catch
                        {
                            targetSubRoot = null;
                        }
                    }

                    if (targetSubRoot != null)
                    {
                        player.SetCurrentSub(targetSubRoot);
                        player.EnterInterior(targetSubRoot);
                    }
                    else if (!string.IsNullOrEmpty(targetPlayer.CurrentInteriorId))
                    {
                        try
                        {
                            var interiorGo = Network.Identifier.GetGameObject(targetPlayer.CurrentInteriorId, true);
                            var targetInterior = interiorGo != null ? interiorGo.GetComponent<global::IInteriorSpace>() : null;
                            if (targetInterior is global::SeaTruckSegment targetSeaTruckSegment)
                            {
                                if (targetPlayer.IsInSeaTruck)
                                {
                                    targetSeaTruckSegment.Enter(player);
                                    player.SetPosition(safePos);
                                    enteredTargetSeaTruck = true;
                                }
                            }
                            else if (targetInterior != null)
                            {
                                player.EnterInterior(targetInterior);
                            }
                        }
                        catch
                        {
                            // Fall back to exterior state if target interior cannot be resolved
                        }
                    }
                }

                // Pre-update underwater state for both < oceanLevel and >= oceanLevel when outside an interior
                // to eliminate both the [-0.1f, 0f] and [0f, +0.42f] hysteresis deadzones in Player.UpdateIsUnderwater()
                float oceanLevel = global::Ocean.GetOceanLevel();
                bool isOutsideDrySpace = player.GetCurrentSub() == null && player.currentInterior == null && !GetPrecursorOutOfWater(player);
                if (isOutsideDrySpace)
                {
                    bool belowOcean = safePos.y < oceanLevel;
                    player.isUnderwater.Update(belowOcean);
                    player.isUnderwaterForSwimming.Update(belowOcean);
                }

                if (!enteredTargetSeaTruck)
                {
                    RefreshPlayerControllerAndMotors(player);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error in FinalizePlayerTeleportState: {ex}");
            }
        }

        public static void RefreshPlayerControllerAndMotors(global::Player player)
        {
            if (player == null)
            {
                return;
            }

            ClearGroundMotorPlatformState(player);

            player.UpdateIsUnderwater();
            InvokeUpdateMotorMode(player);

            if (player.playerController != null)
            {
                // Enable PlayerController first so base.enabled == true when HandleControllerState() enables activeController
                player.playerController.SetEnabled(false);
                player.playerController.SetEnabled(true);

                // HandleControllerStateAfterDeserialization calls HandleUnderWaterState() (updating underWater & inVehicle fields)
                // followed by HandleControllerState() (enabling the active motor and restoring Rigidbody non-kinematic & CapsuleCollider state)
                var handleAfterDeserialization = playerControllerHandleStateAfterDeserializationMethod ??=
                    AccessTools.Method(typeof(global::PlayerController), "HandleControllerStateAfterDeserialization");
                if (handleAfterDeserialization != null)
                {
                    handleAfterDeserialization.Invoke(player.playerController, null);
                }
                else
                {
                    (playerControllerHandleUnderWaterStateMethod ??= AccessTools.Method(typeof(global::PlayerController), "HandleUnderWaterState"))?.Invoke(player.playerController, null);
                    (playerControllerHandleStateMethod ??= AccessTools.Method(typeof(global::PlayerController), "HandleControllerState"))?.Invoke(player.playerController, null);
                }

                if (player.playerController.activeController != null)
                {
                    player.playerController.activeController.SetUnderWater(player.IsUnderwater());
                }

                player.playerController.ForceControllerSize();
            }
        }

        public static void HandlePrecursorMoonPoolTriggerUpdate(global::PrecursorMoonPoolTrigger trigger)
        {
            if (trigger == null || (!IsInTeleportGracePeriod() && !IsGotoMovingPlayer()))
            {
                return;
            }

            try
            {
                var checkPlayerField = precursorCheckPlayerField ??= AccessTools.Field(typeof(global::PrecursorMoonPoolTrigger), "checkPlayer");
                var trackedPlayer = checkPlayerField?.GetValue(trigger) as global::Player;
                if (trackedPlayer != null)
                {
                    var withinBoundsMethod = precursorCheckPlayerWithinBoundsMethod ??= AccessTools.Method(typeof(global::PrecursorMoonPoolTrigger), "CheckPlayerWithinBounds");
                    bool withinBounds = withinBoundsMethod != null && (bool)withinBoundsMethod.Invoke(trigger, null);
                    if (!withinBounds)
                    {
                        trackedPlayer.SetPrecursorOutOfWater(false);
                        checkPlayerField.SetValue(trigger, null);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error in HandlePrecursorMoonPoolTriggerUpdate: {ex}");
            }
        }

        public static IEnumerator WaitForGotoAndFinalizeCoroutine(global::Player player, global::GotoConsoleCommand gotoCmd, Vector3 targetPos)
        {
            // Yield one frame so GotoLocation coroutine can set movingPlayer = true
            yield return null;

            float maxWaitTime = 20f;
            float elapsed = 0f;
            while (gotoCmd != null && IsGotoMovingPlayer() && elapsed < maxWaitTime)
            {
                if (player != null)
                {
                    MarkTeleportTimestamp(LastPreTeleportPosition, targetPos, targetIsExterior: true);
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (player != null)
            {
                Vector3 finalPos = player.transform.position;
                MarkTeleportTimestamp(LastPreTeleportPosition, finalPos, targetIsExterior: true);
                FinalizePlayerTeleportState(player, finalPos, null);
                player.StartCoroutine(VerifyPostTeleportStateCoroutine(player, finalPos, null));
            }
        }

        private static void ResetSeaTruckAnimations(global::SeaTruckSegment segment)
        {
            if (segment == null)
            {
                return;
            }

            ResetSingleSeaTruckAnimation(segment.seatruckanimation);
            var firstSegment = GetFirstSeaTruckSegment(segment);
            if (firstSegment != null && firstSegment != segment)
            {
                ResetSingleSeaTruckAnimation(firstSegment.seatruckanimation);
            }
        }

        private static void ResetSingleSeaTruckAnimation(global::SeaTruckAnimation animation)
        {
            if (animation == null)
            {
                return;
            }

            try
            {
                // Clear _currentAnimation BEFORE ending any active cinematic so SeaTruckAnimation_OnPlayerCinematicModeEnd
                // does not see Animation.EndPilot and re-enable SeaTruckCustomMotor
                var animField = seaTruckAnimationCurrentAnimField ??= AccessTools.Field(typeof(global::SeaTruckAnimation), "_currentAnimation");
                animField?.SetValue(animation, global::SeaTruckAnimation.Animation.Idle);

                if (animation.animToController != null)
                {
                    foreach (var entry in animation.animToController)
                    {
                        if (entry.controller != null && entry.controller.IsCinematicModeActive())
                        {
                            entry.controller.BreakCinematic();
                        }
                    }
                }

                animField?.SetValue(animation, global::SeaTruckAnimation.Animation.Idle);
            }
            catch
            {
                // Ignore animation cleanup errors
            }
        }

        private static global::SeaTruckSegment GetFirstSeaTruckSegment(global::SeaTruckSegment segment)
        {
            if (segment == null)
            {
                return null;
            }

            try
            {
                var method = seaTruckGetFirstSegmentMethod ??= AccessTools.Method(typeof(global::SeaTruckSegment), "GetFirstSegment");
                return method?.Invoke(segment, null) as global::SeaTruckSegment ?? segment;
            }
            catch
            {
                return segment;
            }
        }

        private static void InvokeUpdateMotorMode(global::Player player)
        {
            if (player == null)
            {
                return;
            }

            try
            {
                var method = playerUpdateMotorModeMethod ??= AccessTools.Method(typeof(global::Player), "UpdateMotorMode");
                method?.Invoke(player, null);
            }
            catch
            {
                // Ignore reflection errors
            }
        }

        private static bool GetPrecursorOutOfWater(global::Player player)
        {
            if (player == null)
            {
                return false;
            }

            return player.forceWalkMotorMode;
        }

        private static void SetPrecursorOutOfWater(global::Player player, bool value)
        {
            if (player == null)
            {
                return;
            }

            try
            {
                player.SetPrecursorOutOfWater(value);
                player.forceWalkMotorMode = value;
            }
            catch
            {
                player.forceWalkMotorMode = value;
            }
        }

        private static void DeactivateSeaTruckCustomMotor(global::Player player)
        {
            if (player == null)
            {
                return;
            }

            try
            {
                if (Network.IsMultiplayerActive)
                {
                    Network.Session.IsInSeaTruck = false;
                }
            }
            catch
            {
                // Ignore if Network.Session is not initialized
            }

            try
            {
                if (player.TryGetComponent<SeaTruckCustomMotor>(out var customMotor) && customMotor != null)
                {
                    customMotor.SetSeaTruckSegment(null);
                    if (customMotor.IsActive || customMotor.enabled)
                    {
                        customMotor.SetActive(false, isForce: true, isKeepParent: false);
                    }
                }
            }
            catch
            {
                // Ignore if component not attached
            }

            try
            {
                // SeaTruckCustomMotor.SetActive(false) sets Physics.autoSyncTransforms = false; restore it so transform teleports sync to PhysX
                Physics.autoSyncTransforms = true;
            }
            catch
            {
                // Ignore outside Unity runtime
            }
        }

        private static void ClearGroundMotorPlatformState(global::Player player)
        {
            if (player?.groundMotor == null)
            {
                return;
            }

            player.groundMotor.OnTeleport();
            if (player.groundMotor.movingPlatform != null)
            {
                player.groundMotor.movingPlatform.activePlatform = null;
                player.groundMotor.movingPlatform.hitPlatform = null;
                player.groundMotor.movingPlatform.activeLocalPoint = Vector3.zero;
                player.groundMotor.movingPlatform.activeGlobalPoint = Vector3.zero;
                player.groundMotor.movingPlatform.platformVelocity = Vector3.zero;
            }
        }

        private static IEnumerator VerifyPostTeleportStateCoroutine(global::Player player, Vector3 safePos, ZeroPlayer targetPlayer)
        {
            bool expectExterior = targetPlayer != null ? !IsTargetPlayerInInterior(targetPlayer) : true;

            // Check across the next several FixedUpdate steps to catch any delayed PhysX trigger or motor callbacks
            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForFixedUpdate();

                if (player == null)
                {
                    yield break;
                }

                try
                {
                    bool needsRefresh = false;

                    if (expectExterior && player.TryGetComponent<SeaTruckCustomMotor>(out var customMotor) && customMotor != null && customMotor.IsActive)
                    {
                        DeactivateSeaTruckCustomMotor(player);
                        needsRefresh = true;
                    }

                    if (expectExterior)
                    {
                        if (player.currentInterior != null)
                        {
                            var interiorGo = player.currentInterior.GetGameObject();
                            if (interiorGo == null || (player.transform.position - interiorGo.transform.position).sqrMagnitude > 36f || targetPlayer != null)
                            {
                                player.ExitCurrentInterior();
                                needsRefresh = true;
                            }
                        }

                        if (player.GetCurrentSub() != null)
                        {
                            var subRoot = player.GetCurrentSub();
                            if (subRoot == null || (player.transform.position - subRoot.transform.position).sqrMagnitude > 2500f || targetPlayer != null)
                            {
                                player.SetCurrentSub(null);
                                needsRefresh = true;
                            }
                        }

                        if (player.forceWalkMotorMode)
                        {
                            SetPrecursorOutOfWater(player, false);
                            needsRefresh = true;
                        }

                        if (player.transform.parent != null)
                        {
                            player.transform.parent = null;
                            needsRefresh = true;
                        }
                    }

                    if (needsRefresh || (i == 0 && expectExterior))
                    {
                        RefreshPlayerControllerAndMotors(player);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[MoreTpCommands] Error in VerifyPostTeleportStateCoroutine: {ex}");
                }
            }
        }

        public static string GetActivePlayerNames()
        {
            try
            {
                var players = ZeroPlayer.GetAllPlayers();
                if (players == null || players.Count == 0)
                {
                    string name = Tools.GetLoggedInName();
                    return string.IsNullOrEmpty(name) ? "Local Player" : name;
                }

                var list = new List<string>();
                foreach (var p in players)
                {
                    if (p != null)
                    {
                        string nick = string.IsNullOrEmpty(p.NickName) ? "Unknown" : p.NickName;
                        if (p.IsMine)
                        {
                            nick += " (You)";
                        }
                        list.Add(nick);
                    }
                }

                return string.Join(", ", list);
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}


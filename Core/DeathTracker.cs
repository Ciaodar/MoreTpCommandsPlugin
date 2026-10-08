namespace Subnautica.MoreTpCommands.Core
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using Newtonsoft.Json;
    using Subnautica.API.Features;
    using UnityEngine;

    public static class DeathTracker
    {
        public static Vector3? LastDeathPosition { get; private set; }

        public static bool HasDeathPosition => LastDeathPosition.HasValue;

        public static DateTime? LastDeathTime { get; private set; }

        private static string StorageFilePath
        {
            get
            {
                try
                {
                    string path = Paths.GetLauncherGamePath("MoreTpCommands_Death.json", addDS: false);
                    return path;
                }
                catch
                {
                    return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MoreTpCommands_Death.json");
                }
            }
        }

        public static void RecordDeath(Vector3 position)
        {
            try
            {
                LastDeathPosition = position;
                LastDeathTime = DateTime.UtcNow;

                Log.Info($"[MoreTpCommands] Recorded death position: {position}");
                SaveToDisk(position);

                ErrorMessage.AddMessage("<color=#FFD700>[MoreTpCommands]</color> Death location saved. Type <color=#00FFFF>back</color> in console or chat to return here.");
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Error in RecordDeath: {ex}");
            }
        }

        public static void Initialize()
        {
            LoadFromDisk();
        }

        public static void Clear()
        {
            LastDeathPosition = null;
            LastDeathTime = null;
        }

        private static void SaveToDisk(Vector3 position)
        {
            try
            {
                var data = new DeathData
                {
                    X = position.x,
                    Y = position.y,
                    Z = position.z,
                    Timestamp = DateTime.UtcNow
                };

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(StorageFilePath, json);
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Failed to save death position to disk: {ex.Message}");
            }
        }

        private static void LoadFromDisk()
        {
            try
            {
                if (File.Exists(StorageFilePath))
                {
                    string json = File.ReadAllText(StorageFilePath);
                    var data = JsonConvert.DeserializeObject<DeathData>(json);
                    if (data != null)
                    {
                        LastDeathPosition = new Vector3(data.X, data.Y, data.Z);
                        LastDeathTime = data.Timestamp;
                        Log.Info($"[MoreTpCommands] Loaded last death position from disk: {LastDeathPosition}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MoreTpCommands] Failed to load death position from disk: {ex.Message}");
            }
        }

        private class DeathData
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}

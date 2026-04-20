using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Utilities;
using ComputerVision_LED_Console.Vision;

namespace ComputerVision_LED_Console.App
{
    public static class ConfigStore
    {
        public const string FileName = "config_ComputerVisionLed.txt";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        public static string FilePath => Path.Combine(AppContext.BaseDirectory, FileName);

        public static bool TryLoad(out ConfigSnapshot snapshot)
        {
            string path = FilePath;
            if (!File.Exists(path))
            {
                Logger.Info($"Config file not found at {path}; using defaults.");
                snapshot = new ConfigSnapshot();
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<ConfigSnapshot>(json, JsonOptions);
                if (loaded is null)
                {
                    Logger.Warn($"Config file {path} was empty; using defaults.");
                    snapshot = new ConfigSnapshot();
                    return false;
                }
                loaded.App ??= new AppConfig();
                loaded.Markers ??= new List<MarkerSnapshot>();
                snapshot = loaded;
                Logger.Info($"Loaded config from {path} ({snapshot.Markers.Count} marker(s)).");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to parse {path}: {ex.Message}. Using defaults.");
                snapshot = new ConfigSnapshot();
                return false;
            }
        }

        public static void Save(AppConfig config, IReadOnlyList<RoiConfig> markers)
        {
            var snapshot = new ConfigSnapshot { App = config };
            foreach (var m in markers)
            {
                snapshot.Markers.Add(new MarkerSnapshot
                {
                    Id = m.Id,
                    CenterX = m.CenterX,
                    CenterY = m.CenterY,
                    Radius = m.Radius,
                    OnThreshold = m.OnThreshold,
                    OffThreshold = m.OffThreshold,
                });
            }

            string path = FilePath;
            try
            {
                string json = JsonSerializer.Serialize(snapshot, JsonOptions);
                File.WriteAllText(path, json);
                Logger.Info($"Saved config to {path} ({snapshot.Markers.Count} marker(s)).");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save config to {path}: {ex.Message}");
            }
        }
    }
}

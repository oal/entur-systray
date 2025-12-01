using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace EnturSystray;

public class Schedule
{
    public TimeOnly StartTime { get; set; } = new TimeOnly(0, 0);
    public TimeOnly EndTime { get; set; } = new TimeOnly(23, 59);
    public List<DayOfWeek> Days { get; set; } = new List<DayOfWeek>
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
        DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    };
}

public class TrayIconConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string StopPlaceId { get; set; } = "";
    public string? QuayId { get; set; }
    public string? LineFilter { get; set; }
    public string? DestinationFilter { get; set; }
    public string TextColor { get; set; } = "#FFFF00"; // Yellow default
    public string DisplayName { get; set; } = "";
    public Schedule? Schedule { get; set; }  // null = always show

    public bool IsActiveNow()
    {
        if (Schedule == null) return true;  // No schedule = always active

        var now = DateTime.Now;
        var currentTime = TimeOnly.FromDateTime(now);
        var currentDay = now.DayOfWeek;

        if (!Schedule.Days.Contains(currentDay)) return false;

        // Handle overnight spans (e.g., 22:00 - 06:00)
        if (Schedule.StartTime <= Schedule.EndTime)
        {
            return currentTime >= Schedule.StartTime && currentTime <= Schedule.EndTime;
        }
        else
        {
            return currentTime >= Schedule.StartTime || currentTime <= Schedule.EndTime;
        }
    }
}

public static class IconColors
{
    public static readonly Dictionary<string, string> Presets = new()
    {
        { "Yellow", "#FFFF00" },
        { "Cyan", "#00FFFF" },
        { "Lime", "#00FF00" },
        { "Orange", "#FFA500" },
        { "Magenta", "#FF00FF" },
        { "White", "#FFFFFF" },
        { "Red", "#FF4444" },
        { "Sky Blue", "#87CEEB" }
    };

    public static System.Drawing.Color ParseColor(string hexColor)
    {
        return System.Drawing.ColorTranslator.FromHtml(hexColor);
    }
}

public class Config
{
    private static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EnturSystray");

    private static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public const int MaxIcons = 5;

    public List<TrayIconConfig> Icons { get; set; } = new();
    public int RefreshIntervalSeconds { get; set; } = 30;
    public string Language { get; set; } = "auto";  // "auto", "en", "nb-NO"
    public bool CheckForUpdates { get; set; } = true;

    public static Config Load()
    {
        if (!File.Exists(ConfigPath))
        {
            return new Config();
        }

        var json = File.ReadAllText(ConfigPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Check if this is the old format (has StopPlaceId at root level)
        var jsonNode = JsonNode.Parse(json);
        if (jsonNode?["StopPlaceId"] != null)
        {
            // Migrate from old format
            return MigrateFromOldFormat(json, options);
        }

        return JsonSerializer.Deserialize<Config>(json, options) ?? new Config();
    }

    private static Config MigrateFromOldFormat(string json, JsonSerializerOptions options)
    {
        var oldConfig = JsonSerializer.Deserialize<OldConfig>(json, options);
        if (oldConfig == null)
        {
            return new Config();
        }

        var newConfig = new Config
        {
            RefreshIntervalSeconds = oldConfig.RefreshIntervalSeconds,
            Icons = new List<TrayIconConfig>
            {
                new TrayIconConfig
                {
                    StopPlaceId = oldConfig.StopPlaceId,
                    QuayId = oldConfig.QuayId,
                    LineFilter = oldConfig.LineFilter,
                    DestinationFilter = oldConfig.DestinationFilter,
                    TextColor = "#FFFF00", // Yellow default
                    DisplayName = BuildDisplayName(oldConfig.LineFilter, oldConfig.DestinationFilter)
                }
            }
        };

        // Save in new format
        newConfig.Save();
        return newConfig;
    }

    private static string BuildDisplayName(string? lineFilter, string? destinationFilter)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(lineFilter))
            parts.Add($"Line {lineFilter}");
        if (!string.IsNullOrEmpty(destinationFilter))
            parts.Add(destinationFilter);
        return parts.Count > 0 ? string.Join(" - ", parts) : "Bus Departure";
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDirectory);
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(ConfigPath, json);
    }

    public static bool IsValidStopPlaceId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        return Regex.IsMatch(id, @"^NSR:StopPlace:\d+$");
    }

    public static bool IsValidQuayId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return true; // null/empty is valid (means all quays)
        return Regex.IsMatch(id, @"^NSR:Quay:\d+$");
    }

    public static bool IsValidRefreshInterval(int seconds)
    {
        return seconds >= 10;
    }

    // Old config format for migration
    private class OldConfig
    {
        public string StopPlaceId { get; set; } = "";
        public string? QuayId { get; set; }
        public string? LineFilter { get; set; }
        public string? DestinationFilter { get; set; }
        public int RefreshIntervalSeconds { get; set; } = 30;
    }
}

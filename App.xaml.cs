using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using EnturSystray.Localization;
using EnturSystray.Resources;
using Forms = System.Windows.Forms;

namespace EnturSystray;

public partial class App : System.Windows.Application
{
    private const string DefaultIconId = "__default__";
    private readonly Dictionary<string, Forms.NotifyIcon> _trayIcons = new();
    private readonly Dictionary<string, List<DepartureInfo>?> _iconDepartures = new();
    private DispatcherTimer? _timer;
    private DispatcherTimer? _countdownTimer;
    private Config? _config;
    private EnturService? _enturService;
    private DateTime _lastFetch = DateTime.MinValue;
    private Forms.NotifyIcon? _defaultTrayIcon;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load configuration
        _config = Config.Load();

        // Initialize localization BEFORE creating any UI
        LocalizationManager.Initialize(_config.Language);

        _enturService = new EnturService();

        // Create tray icons based on config
        RecreateAllTrayIcons();

        // Fetch departures immediately
        await FetchDeparturesAsync();

        // Start the API refresh timer
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_config.RefreshIntervalSeconds)
        };
        _timer.Tick += async (s, e) => await FetchDeparturesAsync();
        _timer.Start();

        // Start a 1-second countdown timer to update the display
        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += (s, e) => UpdateAllDisplays();
        _countdownTimer.Start();
    }

    private void RecreateAllTrayIcons()
    {
        // Dispose existing icons
        foreach (var icon in _trayIcons.Values)
        {
            icon.Visible = false;
            icon.Dispose();
        }
        _trayIcons.Clear();
        _iconDepartures.Clear();

        // Dispose and recreate default icon
        if (_defaultTrayIcon != null)
        {
            _defaultTrayIcon.Visible = false;
            _defaultTrayIcon.Dispose();
            _defaultTrayIcon = null;
        }

        if (_config == null) return;

        // Always create the default icon (will be shown/hidden based on schedule)
        _defaultTrayIcon = new Forms.NotifyIcon
        {
            Text = Strings.Tooltip_NoScheduledDepartures,
            Visible = false,
            ContextMenuStrip = CreateContextMenu(),
            Icon = IconGenerator.CreateDefaultIcon(DefaultIconId)
        };

        // If no icons configured, show the default icon
        if (_config.Icons.Count == 0)
        {
            _defaultTrayIcon.Text = Strings.Tooltip_ClickSettingsToAdd;
            _defaultTrayIcon.Visible = true;
            return;
        }

        // Create icons for each configured icon (initially hidden)
        foreach (var iconConfig in _config.Icons)
        {
            var tooltip = !string.IsNullOrEmpty(iconConfig.DisplayName)
                ? iconConfig.DisplayName
                : Strings.Tooltip_Loading;
            var color = IconColors.ParseColor(iconConfig.TextColor);
            var trayIcon = CreateTrayIcon(iconConfig.Id, tooltip, color);
            _trayIcons[iconConfig.Id] = trayIcon;
            trayIcon.Visible = false; // Will be updated based on schedule
            UpdateTrayIcon(iconConfig.Id, "?", color);
        }

        // Initial visibility update
        UpdateIconVisibility();
    }

    private void UpdateIconVisibility()
    {
        if (_config == null) return;

        bool anyIconActive = false;

        foreach (var iconConfig in _config.Icons)
        {
            if (_trayIcons.TryGetValue(iconConfig.Id, out var trayIcon))
            {
                bool isActive = iconConfig.IsActiveNow();
                trayIcon.Visible = isActive;
                if (isActive) anyIconActive = true;
            }
        }

        // Show default icon only when no icons are active
        if (_defaultTrayIcon != null)
        {
            _defaultTrayIcon.Visible = !anyIconActive;
        }
    }

    private Forms.NotifyIcon CreateTrayIcon(string id, string tooltip, Color color)
    {
        var trayIcon = new Forms.NotifyIcon
        {
            Text = tooltip.Length > 63 ? tooltip[..63] : tooltip,
            Visible = true,
            ContextMenuStrip = CreateContextMenu()
        };

        trayIcon.MouseClick += (sender, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                var iconConfig = _config?.Icons.FirstOrDefault(i => i.Id == id);
                if (iconConfig != null && !string.IsNullOrEmpty(iconConfig.StopPlaceId))
                {
                    var url = $"https://entur.no/nearby-stop-place-detail?id={iconConfig.StopPlaceId}";
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
            }
        };

        return trayIcon;
    }

    private Forms.ContextMenuStrip CreateContextMenu()
    {
        var menu = new Forms.ContextMenuStrip();

        var refreshItem = new Forms.ToolStripMenuItem(Strings.Menu_RefreshNow);
        refreshItem.Click += async (s, e) => await FetchDeparturesAsync();
        menu.Items.Add(refreshItem);

        var settingsItem = new Forms.ToolStripMenuItem(Strings.Menu_Settings);
        settingsItem.Click += (s, e) => ShowSettingsDialog();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new Forms.ToolStripSeparator());

        var exitItem = new Forms.ToolStripMenuItem(Strings.Menu_Exit);
        exitItem.Click += (s, e) => Shutdown();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void ShowSettingsDialog()
    {
        var settingsWindow = new SettingsWindow(_config!);
        if (settingsWindow.ShowDialog() == true && settingsWindow.ResultConfig != null)
        {
            ApplyNewConfig(settingsWindow.ResultConfig);
        }
    }

    private async void ApplyNewConfig(Config newConfig)
    {
        var languageChanged = _config?.Language != newConfig.Language;
        _config = newConfig;

        if (languageChanged)
        {
            // Restart the application to apply new language
            RestartApplication();
            return;
        }

        if (_timer != null)
        {
            _timer.Interval = TimeSpan.FromSeconds(_config.RefreshIntervalSeconds);
        }

        // Recreate all tray icons to reflect new configuration
        RecreateAllTrayIcons();

        await FetchDeparturesAsync();
    }

    private void RestartApplication()
    {
        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath))
        {
            Process.Start(exePath);
        }
        Shutdown();
    }

    private async Task FetchDeparturesAsync()
    {
        if (_enturService == null || _config == null) return;

        if (_config.Icons.Count == 0)
        {
            // No icons configured
            return;
        }

        var results = await _enturService.GetDeparturesForAllIconsAsync(_config.Icons);

        foreach (var kvp in results)
        {
            _iconDepartures[kvp.Key] = kvp.Value;
        }

        _lastFetch = DateTime.Now;
        UpdateAllDisplays();
    }

    private void UpdateAllDisplays()
    {
        if (_config == null) return;

        // Update visibility based on schedules (this runs every second)
        UpdateIconVisibility();

        if (_config.Icons.Count == 0)
        {
            // No icons configured, default icon is already visible
            return;
        }

        foreach (var iconConfig in _config.Icons)
        {
            // Only update display for active icons
            if (iconConfig.IsActiveNow())
            {
                UpdateIconDisplay(iconConfig);
            }
        }
    }

    private void UpdateIconDisplay(TrayIconConfig iconConfig)
    {
        if (!_trayIcons.TryGetValue(iconConfig.Id, out var trayIcon))
            return;

        var color = IconColors.ParseColor(iconConfig.TextColor);

        if (!_iconDepartures.TryGetValue(iconConfig.Id, out var departures) ||
            departures == null || departures.Count == 0)
        {
            UpdateTrayIcon(iconConfig.Id, "--", color);
            trayIcon.Text = $"{iconConfig.DisplayName}\n{Strings.Tooltip_NoDepartures}";
            return;
        }

        // Recalculate minutes based on current time
        var updatedDepartures = departures
            .Select(d => d with { MinutesUntilDeparture = Math.Max(0, d.MinutesUntilDeparture - (int)(DateTime.Now - _lastFetch).TotalMinutes) })
            .Where(d => d.MinutesUntilDeparture >= 0)
            .ToList();

        if (updatedDepartures.Count == 0)
        {
            UpdateTrayIcon(iconConfig.Id, "--", color);
            trayIcon.Text = $"{iconConfig.DisplayName}\n{Strings.Tooltip_NoUpcoming}";
            return;
        }

        var nextDeparture = updatedDepartures[0];
        var displayMinutes = Math.Min(99, nextDeparture.MinutesUntilDeparture);
        UpdateTrayIcon(iconConfig.Id, displayMinutes.ToString(), color);

        // Build tooltip with icon name and next 3 departures
        var displayName = !string.IsNullOrEmpty(iconConfig.DisplayName)
            ? iconConfig.DisplayName
            : iconConfig.DestinationFilter ?? Strings.Default_Departures;
        var tooltip = $"{displayName}\n";
        var count = Math.Min(3, updatedDepartures.Count);
        for (int i = 0; i < count; i++)
        {
            var dep = updatedDepartures[i];
            var realtimeIndicator = dep.IsRealtime ? Strings.Tooltip_Realtime : "";
            tooltip += $"{dep.LineCode}: {dep.MinutesUntilDeparture} min{realtimeIndicator}\n";
        }
        // NotifyIcon.Text has a 64 character limit
        trayIcon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip.TrimEnd();
    }

    private void UpdateTrayIcon(string iconId, string text, Color color)
    {
        if (_trayIcons.TryGetValue(iconId, out var trayIcon))
        {
            var icon = IconGenerator.CreateBadgeIcon(text, color, iconId);
            trayIcon.Icon = icon;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _timer?.Stop();
        _countdownTimer?.Stop();
        _enturService?.Dispose();

        foreach (var trayIcon in _trayIcons.Values)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
        }
        _trayIcons.Clear();

        if (_defaultTrayIcon != null)
        {
            _defaultTrayIcon.Visible = false;
            _defaultTrayIcon.Dispose();
            _defaultTrayIcon = null;
        }

        IconGenerator.Cleanup();
        base.OnExit(e);
    }
}

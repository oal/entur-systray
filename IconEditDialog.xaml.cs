using System.Windows;
using EnturSystray.Localization;
using EnturSystray.Resources;
using WpfMedia = System.Windows.Media;
using Input = System.Windows.Input;

namespace EnturSystray;

public partial class IconEditDialog : Window
{
    private readonly EnturService _enturService;
    private readonly TrayIconConfig _originalConfig;

    public TrayIconConfig? ResultConfig { get; private set; }

    // Color item for ComboBox with localized name
    private class ColorItem
    {
        public string Key { get; }
        public string DisplayName { get; }
        public string HexColor { get; }
        public WpfMedia.SolidColorBrush Brush { get; }

        public ColorItem(string key, string hexColor)
        {
            Key = key;
            HexColor = hexColor;
            DisplayName = LocalizationManager.GetColorName(key);
            try
            {
                var color = (WpfMedia.Color)WpfMedia.ColorConverter.ConvertFromString(hexColor);
                Brush = new WpfMedia.SolidColorBrush(color);
            }
            catch
            {
                Brush = WpfMedia.Brushes.Yellow;
            }
        }
    }

    // Wrapper classes for ComboBox display
    private class StopItem
    {
        public StopInfo Stop { get; }
        public string DisplayName => Stop.Locality != null
            ? $"{Stop.Name} ({Stop.Locality})"
            : Stop.Name;

        public StopItem(StopInfo stop) => Stop = stop;
    }

    private class QuayItem
    {
        public QuayInfo? Quay { get; }
        public string DisplayName { get; }

        public QuayItem(QuayInfo? quay, string displayName)
        {
            Quay = quay;
            DisplayName = displayName;
        }
    }

    public IconEditDialog(TrayIconConfig config)
    {
        InitializeComponent();
        _originalConfig = config;
        _enturService = new EnturService();

        // Set localized button content
        InitializeLocalizedUI();

        // Populate color dropdown with localized names
        foreach (var kvp in IconColors.Presets)
        {
            ColorComboBox.Items.Add(new ColorItem(kvp.Key, kvp.Value));
        }

        // Select current color
        var currentColorIndex = IconColors.Presets.Values.ToList().IndexOf(config.TextColor);
        ColorComboBox.SelectedIndex = currentColorIndex >= 0 ? currentColorIndex : 0;

        // Set display name
        DisplayNameTextBox.Text = config.DisplayName;

        // Initialize schedule UI
        InitializeScheduleUI();
        LoadScheduleFromConfig(config.Schedule);

        // Pre-populate dropdowns if we have existing config
        if (!string.IsNullOrEmpty(config.StopPlaceId))
        {
            LoadCurrentConfiguration();
        }
    }

    private void InitializeLocalizedUI()
    {
        // Set day checkbox labels
        MondayCheckBox.Content = Strings.Day_Mon;
        TuesdayCheckBox.Content = Strings.Day_Tue;
        WednesdayCheckBox.Content = Strings.Day_Wed;
        ThursdayCheckBox.Content = Strings.Day_Thu;
        FridayCheckBox.Content = Strings.Day_Fri;
        SaturdayCheckBox.Content = Strings.Day_Sat;
        SundayCheckBox.Content = Strings.Day_Sun;

        // Set day preset buttons
        WeekdaysButton.Content = Strings.Button_Weekdays;
        WeekendsButton.Content = Strings.Button_Weekends;
        AllDaysButton.Content = Strings.Button_AllDays;
    }

    private void InitializeScheduleUI()
    {
        // Populate hour dropdowns (00-23)
        for (int h = 0; h < 24; h++)
        {
            var hourStr = h.ToString("D2");
            StartHourComboBox.Items.Add(hourStr);
            EndHourComboBox.Items.Add(hourStr);
        }

        // Populate minute dropdowns (00, 15, 30, 45 for convenience, but allow any via typing)
        var minutes = new[] { "00", "15", "30", "45" };
        foreach (var m in minutes)
        {
            StartMinuteComboBox.Items.Add(m);
            EndMinuteComboBox.Items.Add(m);
        }

        // Set defaults
        StartHourComboBox.SelectedIndex = 7;  // 07:00
        StartMinuteComboBox.SelectedIndex = 0;
        EndHourComboBox.SelectedIndex = 9;    // 09:00
        EndMinuteComboBox.SelectedIndex = 0;

        // All days checked by default
        SetAllDaysChecked(true);

        // Schedule disabled by default
        EnableScheduleCheckBox.IsChecked = false;
        UpdateScheduleUIEnabled();
    }

    private void LoadScheduleFromConfig(Schedule? schedule)
    {
        if (schedule == null)
        {
            EnableScheduleCheckBox.IsChecked = false;
            UpdateScheduleUIEnabled();
            return;
        }

        EnableScheduleCheckBox.IsChecked = true;

        // Set time
        StartHourComboBox.SelectedItem = schedule.StartTime.Hour.ToString("D2");
        EndHourComboBox.SelectedItem = schedule.EndTime.Hour.ToString("D2");

        // Find closest minute option or add exact value
        SetMinuteComboBox(StartMinuteComboBox, schedule.StartTime.Minute);
        SetMinuteComboBox(EndMinuteComboBox, schedule.EndTime.Minute);

        // Set days
        MondayCheckBox.IsChecked = schedule.Days.Contains(DayOfWeek.Monday);
        TuesdayCheckBox.IsChecked = schedule.Days.Contains(DayOfWeek.Tuesday);
        WednesdayCheckBox.IsChecked = schedule.Days.Contains(DayOfWeek.Wednesday);
        ThursdayCheckBox.IsChecked = schedule.Days.Contains(DayOfWeek.Thursday);
        FridayCheckBox.IsChecked = schedule.Days.Contains(DayOfWeek.Friday);
        SaturdayCheckBox.IsChecked = schedule.Days.Contains(DayOfWeek.Saturday);
        SundayCheckBox.IsChecked = schedule.Days.Contains(DayOfWeek.Sunday);

        UpdateScheduleUIEnabled();
    }

    private void SetMinuteComboBox(System.Windows.Controls.ComboBox comboBox, int minute)
    {
        var minuteStr = minute.ToString("D2");
        if (!comboBox.Items.Contains(minuteStr))
        {
            comboBox.Items.Add(minuteStr);
        }
        comboBox.SelectedItem = minuteStr;
    }

    private void UpdateScheduleUIEnabled()
    {
        var isEnabled = EnableScheduleCheckBox.IsChecked == true;
        TimeLabel.IsEnabled = isEnabled;
        TimePanel.IsEnabled = isEnabled;
        DaysLabel.IsEnabled = isEnabled;
        DaysPanel.IsEnabled = isEnabled;
    }

    private void EnableScheduleCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateScheduleUIEnabled();
    }

    private void SetAllDaysChecked(bool isChecked)
    {
        MondayCheckBox.IsChecked = isChecked;
        TuesdayCheckBox.IsChecked = isChecked;
        WednesdayCheckBox.IsChecked = isChecked;
        ThursdayCheckBox.IsChecked = isChecked;
        FridayCheckBox.IsChecked = isChecked;
        SaturdayCheckBox.IsChecked = isChecked;
        SundayCheckBox.IsChecked = isChecked;
    }

    private void WeekdaysButton_Click(object sender, RoutedEventArgs e)
    {
        MondayCheckBox.IsChecked = true;
        TuesdayCheckBox.IsChecked = true;
        WednesdayCheckBox.IsChecked = true;
        ThursdayCheckBox.IsChecked = true;
        FridayCheckBox.IsChecked = true;
        SaturdayCheckBox.IsChecked = false;
        SundayCheckBox.IsChecked = false;
    }

    private void WeekendsButton_Click(object sender, RoutedEventArgs e)
    {
        MondayCheckBox.IsChecked = false;
        TuesdayCheckBox.IsChecked = false;
        WednesdayCheckBox.IsChecked = false;
        ThursdayCheckBox.IsChecked = false;
        FridayCheckBox.IsChecked = false;
        SaturdayCheckBox.IsChecked = true;
        SundayCheckBox.IsChecked = true;
    }

    private void AllDaysButton_Click(object sender, RoutedEventArgs e)
    {
        SetAllDaysChecked(true);
    }

    private Schedule? BuildScheduleFromUI()
    {
        if (EnableScheduleCheckBox.IsChecked != true)
        {
            return null;
        }

        var startHour = int.Parse((string)StartHourComboBox.SelectedItem ?? "0");
        var startMinute = int.Parse((string)StartMinuteComboBox.SelectedItem ?? "0");
        var endHour = int.Parse((string)EndHourComboBox.SelectedItem ?? "23");
        var endMinute = int.Parse((string)EndMinuteComboBox.SelectedItem ?? "59");

        var days = new List<DayOfWeek>();
        if (MondayCheckBox.IsChecked == true) days.Add(DayOfWeek.Monday);
        if (TuesdayCheckBox.IsChecked == true) days.Add(DayOfWeek.Tuesday);
        if (WednesdayCheckBox.IsChecked == true) days.Add(DayOfWeek.Wednesday);
        if (ThursdayCheckBox.IsChecked == true) days.Add(DayOfWeek.Thursday);
        if (FridayCheckBox.IsChecked == true) days.Add(DayOfWeek.Friday);
        if (SaturdayCheckBox.IsChecked == true) days.Add(DayOfWeek.Saturday);
        if (SundayCheckBox.IsChecked == true) days.Add(DayOfWeek.Sunday);

        return new Schedule
        {
            StartTime = new TimeOnly(startHour, startMinute),
            EndTime = new TimeOnly(endHour, endMinute),
            Days = days
        };
    }

    private async void LoadCurrentConfiguration()
    {
        StatusText.Text = Strings.Status_LoadingConfig;

        // Load quays for the current stop
        var quays = await _enturService.GetQuaysForStopAsync(_originalConfig.StopPlaceId);

        // Add "(All Quays)" option
        QuayComboBox.Items.Clear();
        QuayComboBox.Items.Add(new QuayItem(null, Strings.Option_AllQuays));
        foreach (var quay in quays)
        {
            var linesText = quay.Lines.Count > 0 ? string.Format(Strings.Default_LinesFormat, string.Join(", ", quay.Lines)) : "";
            QuayComboBox.Items.Add(new QuayItem(quay, $"{quay.Name}{linesText}"));
        }

        // Select current quay if set
        if (!string.IsNullOrEmpty(_originalConfig.QuayId))
        {
            for (int i = 0; i < QuayComboBox.Items.Count; i++)
            {
                if (QuayComboBox.Items[i] is QuayItem item && item.Quay?.Id == _originalConfig.QuayId)
                {
                    QuayComboBox.SelectedIndex = i;
                    break;
                }
            }
        }
        else
        {
            QuayComboBox.SelectedIndex = 0;
        }

        // Load lines and destinations
        await LoadLinesAndDestinations();

        StatusText.Text = Strings.Status_Ready;
    }

    private async Task LoadLinesAndDestinations()
    {
        var quayId = (QuayComboBox.SelectedItem as QuayItem)?.Quay?.Id;
        var stopId = (StopComboBox.SelectedItem as StopItem)?.Stop.Id ?? _originalConfig.StopPlaceId;

        if (string.IsNullOrEmpty(stopId))
        {
            LineComboBox.Items.Clear();
            DestinationComboBox.Items.Clear();
            return;
        }

        var info = await _enturService.GetLinesAndDestinationsAsync(stopId, quayId);

        // Populate Line dropdown
        LineComboBox.Items.Clear();
        LineComboBox.Items.Add(Strings.Option_AllLines);
        foreach (var line in info.Lines)
        {
            LineComboBox.Items.Add(line);
        }

        // Select current line
        if (!string.IsNullOrEmpty(_originalConfig.LineFilter))
        {
            var index = info.Lines.IndexOf(_originalConfig.LineFilter);
            LineComboBox.SelectedIndex = index >= 0 ? index + 1 : 0;
        }
        else
        {
            LineComboBox.SelectedIndex = 0;
        }

        // Populate Destination dropdown
        DestinationComboBox.Items.Clear();
        DestinationComboBox.Items.Add(Strings.Option_AllDestinations);
        foreach (var dest in info.Destinations)
        {
            DestinationComboBox.Items.Add(dest);
        }

        // Select current destination
        if (!string.IsNullOrEmpty(_originalConfig.DestinationFilter))
        {
            var index = info.Destinations.FindIndex(d =>
                d.Contains(_originalConfig.DestinationFilter, StringComparison.OrdinalIgnoreCase));
            DestinationComboBox.SelectedIndex = index >= 0 ? index + 1 : 0;
        }
        else
        {
            DestinationComboBox.SelectedIndex = 0;
        }
    }

    private void UpdateDisplayName()
    {
        // Auto-generate display name if empty
        if (string.IsNullOrWhiteSpace(DisplayNameTextBox.Text))
        {
            var parts = new List<string>();

            if (LineComboBox.SelectedIndex > 0 && LineComboBox.SelectedItem is string line)
            {
                parts.Add(string.Format(Strings.Default_LinePrefix, line));
            }

            if (DestinationComboBox.SelectedIndex > 0 && DestinationComboBox.SelectedItem is string dest)
            {
                parts.Add(dest);
            }

            if (parts.Count == 0 && StopComboBox.SelectedItem is StopItem stop)
            {
                parts.Add(stop.Stop.Name);
            }

            DisplayNameTextBox.Text = parts.Count > 0 ? string.Join(" - ", parts) : "";
        }
    }

    private void SearchTextBox_KeyDown(object sender, Input.KeyEventArgs e)
    {
        if (e.Key == Input.Key.Enter)
        {
            SearchButton_Click(sender, e);
        }
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        var query = SearchTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            ErrorText.Text = Strings.Error_EnterSearchTerm;
            return;
        }

        ErrorText.Text = "";
        StatusText.Text = Strings.Status_Searching;
        SearchButton.IsEnabled = false;

        try
        {
            var stops = await _enturService.SearchStopsAsync(query);

            StopComboBox.Items.Clear();
            if (stops.Count == 0)
            {
                StatusText.Text = Strings.Status_NoStopsFound;
                return;
            }

            foreach (var stop in stops)
            {
                StopComboBox.Items.Add(new StopItem(stop));
            }

            StopComboBox.SelectedIndex = 0;
            StatusText.Text = string.Format(Strings.Status_FoundStops, stops.Count);
        }
        catch (Exception ex)
        {
            ErrorText.Text = string.Format(Strings.Error_SearchFailed, ex.Message);
            StatusText.Text = "";
        }
        finally
        {
            SearchButton.IsEnabled = true;
        }
    }

    private async void StopComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (StopComboBox.SelectedItem is not StopItem stopItem)
            return;

        StatusText.Text = Strings.Status_LoadingQuays;

        try
        {
            var quays = await _enturService.GetQuaysForStopAsync(stopItem.Stop.Id);

            QuayComboBox.Items.Clear();
            QuayComboBox.Items.Add(new QuayItem(null, Strings.Option_AllQuays));
            foreach (var quay in quays)
            {
                var linesText = quay.Lines.Count > 0 ? string.Format(Strings.Default_LinesFormat, string.Join(", ", quay.Lines)) : "";
                QuayComboBox.Items.Add(new QuayItem(quay, $"{quay.Name}{linesText}"));
            }
            QuayComboBox.SelectedIndex = 0;

            StatusText.Text = string.Format(Strings.Status_LoadedQuays, quays.Count);
        }
        catch (Exception ex)
        {
            ErrorText.Text = string.Format(Strings.Error_LoadQuaysFailed, ex.Message);
            StatusText.Text = "";
        }
    }

    private async void QuayComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (QuayComboBox.SelectedItem == null)
            return;

        await LoadLinesAndDestinations();
    }

    private void LineComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateDisplayName();
    }

    private void DestinationComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateDisplayName();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";

        // Get stop ID
        string stopPlaceId;
        if (StopComboBox.SelectedItem is StopItem stopItem)
        {
            stopPlaceId = stopItem.Stop.Id;
        }
        else
        {
            stopPlaceId = _originalConfig.StopPlaceId;
        }

        // Validate stop ID
        if (!Config.IsValidStopPlaceId(stopPlaceId))
        {
            ErrorText.Text = Strings.Error_SelectValidStop;
            return;
        }

        // Get quay ID
        string? quayId = null;
        if (QuayComboBox.SelectedItem is QuayItem quayItem && quayItem.Quay != null)
        {
            quayId = quayItem.Quay.Id;
        }

        // Validate quay ID
        if (!Config.IsValidQuayId(quayId))
        {
            ErrorText.Text = Strings.Error_InvalidQuayId;
            return;
        }

        // Get line filter
        string? lineFilter = null;
        if (LineComboBox.SelectedIndex > 0 && LineComboBox.SelectedItem is string line)
        {
            lineFilter = line;
        }

        // Get destination filter
        string? destinationFilter = null;
        if (DestinationComboBox.SelectedIndex > 0 && DestinationComboBox.SelectedItem is string dest)
        {
            destinationFilter = dest;
        }

        // Get color
        var textColor = "#FFFF00"; // Yellow default
        if (ColorComboBox.SelectedItem is ColorItem colorItem)
        {
            textColor = colorItem.HexColor;
        }

        // Get display name
        var displayName = DisplayNameTextBox.Text.Trim();
        if (string.IsNullOrEmpty(displayName))
        {
            // Auto-generate if still empty
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(lineFilter))
                parts.Add(string.Format(Strings.Default_LinePrefix, lineFilter));
            if (!string.IsNullOrEmpty(destinationFilter))
                parts.Add(destinationFilter);
            displayName = parts.Count > 0 ? string.Join(" - ", parts) : Strings.Default_BusDeparture;
        }

        // Build schedule from UI
        var schedule = BuildScheduleFromUI();

        // Validate schedule if enabled
        if (schedule != null && schedule.Days.Count == 0)
        {
            ErrorText.Text = Strings.Error_SelectAtLeastOneDay;
            return;
        }

        // Create result config
        ResultConfig = new TrayIconConfig
        {
            Id = _originalConfig.Id, // Preserve ID
            StopPlaceId = stopPlaceId,
            QuayId = quayId,
            LineFilter = lineFilter,
            DestinationFilter = destinationFilter,
            TextColor = textColor,
            DisplayName = displayName,
            Schedule = schedule
        };

        DialogResult = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _enturService.Dispose();
        base.OnClosed(e);
    }
}

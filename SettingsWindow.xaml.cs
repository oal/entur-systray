using System.Windows;
using EnturSystray.Localization;
using EnturSystray.Resources;
using WpfMedia = System.Windows.Media;

namespace EnturSystray;

public partial class SettingsWindow : Window
{
    private readonly List<TrayIconConfig> _icons;
    private readonly Config _currentConfig;

    public Config? ResultConfig { get; private set; }

    // Language item for ComboBox
    private class LanguageItem
    {
        public string Code { get; }
        public string DisplayName { get; }

        public LanguageItem(string code, string displayName)
        {
            Code = code;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }

    // View model for list display
    private class IconListItem
    {
        public TrayIconConfig Config { get; }
        public string DisplayText
        {
            get
            {
                var name = !string.IsNullOrEmpty(Config.DisplayName)
                    ? Config.DisplayName
                    : Strings.Default_UnconfiguredIcon;

                if (Config.Schedule != null)
                {
                    var scheduleText = FormatSchedule(Config.Schedule);
                    return $"{name} ({scheduleText})";
                }

                return name;
            }
        }

        private static string FormatSchedule(Schedule schedule)
        {
            var timeRange = $"{schedule.StartTime:HH:mm}-{schedule.EndTime:HH:mm}";
            var days = FormatDays(schedule.Days);
            return $"{timeRange} {days}";
        }

        private static string FormatDays(List<DayOfWeek> days)
        {
            if (days.Count == 7) return Strings.Days_Daily;

            var weekdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                                   DayOfWeek.Thursday, DayOfWeek.Friday };
            var weekends = new[] { DayOfWeek.Saturday, DayOfWeek.Sunday };

            if (days.Count == 5 && weekdays.All(d => days.Contains(d)))
                return Strings.Days_MonFri;
            if (days.Count == 2 && weekends.All(d => days.Contains(d)))
                return Strings.Days_SatSun;

            // Abbreviate day names using localized strings
            var abbrevs = days.OrderBy(d => ((int)d + 6) % 7) // Start from Monday
                              .Select(LocalizationManager.GetDayAbbreviation)
                              .ToList();
            return string.Join(", ", abbrevs);
        }

        public WpfMedia.SolidColorBrush ColorBrush => WpfMedia.Brushes.Black;
        public WpfMedia.SolidColorBrush TextColorBrush
        {
            get
            {
                try
                {
                    var color = (WpfMedia.Color)WpfMedia.ColorConverter.ConvertFromString(Config.TextColor);
                    return new WpfMedia.SolidColorBrush(color);
                }
                catch
                {
                    return WpfMedia.Brushes.Yellow;
                }
            }
        }

        public IconListItem(TrayIconConfig config) => Config = config;
    }

    public SettingsWindow(Config currentConfig)
    {
        InitializeComponent();
        _currentConfig = currentConfig;

        // Deep copy the icons list for editing
        _icons = currentConfig.Icons.Select(i => new TrayIconConfig
        {
            Id = i.Id,
            StopPlaceId = i.StopPlaceId,
            QuayId = i.QuayId,
            LineFilter = i.LineFilter,
            DestinationFilter = i.DestinationFilter,
            TextColor = i.TextColor,
            DisplayName = i.DisplayName,
            Schedule = i.Schedule != null ? new Schedule
            {
                StartTime = i.Schedule.StartTime,
                EndTime = i.Schedule.EndTime,
                Days = new List<DayOfWeek>(i.Schedule.Days)
            } : null
        }).ToList();

        RefreshIntervalTextBox.Text = currentConfig.RefreshIntervalSeconds.ToString();

        // Initialize language dropdown
        InitializeLanguageDropdown();

        RefreshIconsList();
        UpdateButtonStates();
    }

    private void InitializeLanguageDropdown()
    {
        LanguageComboBox.Items.Clear();

        // Add language options with localized display names
        LanguageComboBox.Items.Add(new LanguageItem(LocalizationManager.AutoDetect, Strings.Language_Auto));
        LanguageComboBox.Items.Add(new LanguageItem(LocalizationManager.English, Strings.Language_English));
        LanguageComboBox.Items.Add(new LanguageItem(LocalizationManager.NorwegianBokmal, Strings.Language_Norwegian));

        // Select current language
        var currentLang = _currentConfig.Language ?? LocalizationManager.AutoDetect;
        for (int i = 0; i < LanguageComboBox.Items.Count; i++)
        {
            if (LanguageComboBox.Items[i] is LanguageItem item && item.Code == currentLang)
            {
                LanguageComboBox.SelectedIndex = i;
                break;
            }
        }

        if (LanguageComboBox.SelectedIndex < 0)
            LanguageComboBox.SelectedIndex = 0;
    }

    private void RefreshIconsList()
    {
        var selectedIndex = IconsListBox.SelectedIndex;
        IconsListBox.ItemsSource = null;
        IconsListBox.ItemsSource = _icons.Select(i => new IconListItem(i)).ToList();

        if (selectedIndex >= 0 && selectedIndex < _icons.Count)
        {
            IconsListBox.SelectedIndex = selectedIndex;
        }

        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        var hasSelection = IconsListBox.SelectedIndex >= 0;
        var selectedIndex = IconsListBox.SelectedIndex;

        // Set button content with localized strings
        EditButton.Content = Strings.Button_Edit;
        RemoveButton.Content = Strings.Button_Remove;
        MoveUpButton.Content = Strings.Button_MoveUp;
        MoveDownButton.Content = Strings.Button_MoveDown;

        EditButton.IsEnabled = hasSelection;
        RemoveButton.IsEnabled = hasSelection;
        MoveUpButton.IsEnabled = hasSelection && selectedIndex > 0;
        MoveDownButton.IsEnabled = hasSelection && selectedIndex < _icons.Count - 1;

        // Disable Add if at max icons
        AddButton.IsEnabled = _icons.Count < Config.MaxIcons;
        AddButton.Content = _icons.Count >= Config.MaxIcons
            ? string.Format(Strings.Label_MaxIcons, Config.MaxIcons)
            : Strings.Button_Add;
    }

    private void IconsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateButtonStates();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (_icons.Count >= Config.MaxIcons)
        {
            ErrorText.Text = string.Format(Strings.Error_MaxIconsAllowed, Config.MaxIcons);
            return;
        }

        var newIcon = new TrayIconConfig();
        var dialog = new IconEditDialog(newIcon);
        if (dialog.ShowDialog() == true && dialog.ResultConfig != null)
        {
            _icons.Add(dialog.ResultConfig);
            RefreshIconsList();
            IconsListBox.SelectedIndex = _icons.Count - 1;
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (IconsListBox.SelectedItem is not IconListItem item)
            return;

        var index = _icons.FindIndex(i => i.Id == item.Config.Id);
        if (index < 0) return;

        var dialog = new IconEditDialog(item.Config);
        if (dialog.ShowDialog() == true && dialog.ResultConfig != null)
        {
            _icons[index] = dialog.ResultConfig;
            RefreshIconsList();
            IconsListBox.SelectedIndex = index;
        }
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (IconsListBox.SelectedItem is not IconListItem item)
            return;

        var index = _icons.FindIndex(i => i.Id == item.Config.Id);
        if (index >= 0)
        {
            _icons.RemoveAt(index);
            RefreshIconsList();

            // Select nearest item
            if (_icons.Count > 0)
            {
                IconsListBox.SelectedIndex = Math.Min(index, _icons.Count - 1);
            }
        }
    }

    private void MoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        var index = IconsListBox.SelectedIndex;
        if (index <= 0) return;

        var item = _icons[index];
        _icons.RemoveAt(index);
        _icons.Insert(index - 1, item);
        RefreshIconsList();
        IconsListBox.SelectedIndex = index - 1;
    }

    private void MoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        var index = IconsListBox.SelectedIndex;
        if (index < 0 || index >= _icons.Count - 1) return;

        var item = _icons[index];
        _icons.RemoveAt(index);
        _icons.Insert(index + 1, item);
        RefreshIconsList();
        IconsListBox.SelectedIndex = index + 1;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";

        // Validate refresh interval
        if (!int.TryParse(RefreshIntervalTextBox.Text, out int refreshInterval))
        {
            ErrorText.Text = Strings.Error_RefreshIntervalNumber;
            return;
        }

        if (!Config.IsValidRefreshInterval(refreshInterval))
        {
            ErrorText.Text = Strings.Error_RefreshIntervalMin10;
            return;
        }

        // Validate all icons have valid stop IDs
        foreach (var icon in _icons)
        {
            if (!Config.IsValidStopPlaceId(icon.StopPlaceId))
            {
                ErrorText.Text = string.Format(Strings.Error_InvalidStop, icon.DisplayName);
                return;
            }
        }

        // Get selected language
        var selectedLanguage = LocalizationManager.AutoDetect;
        if (LanguageComboBox.SelectedItem is LanguageItem langItem)
        {
            selectedLanguage = langItem.Code;
        }

        // Create and save config
        ResultConfig = new Config
        {
            Icons = _icons,
            RefreshIntervalSeconds = refreshInterval,
            Language = selectedLanguage
        };

        try
        {
            ResultConfig.Save();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = string.Format(Strings.Error_SaveFailed, ex.Message);
        }
    }
}

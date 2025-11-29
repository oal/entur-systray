using System.Globalization;

namespace EnturSystray.Localization;

public static class LocalizationManager
{
    public const string AutoDetect = "auto";
    public const string English = "en";
    public const string NorwegianBokmal = "nb-NO";

    public static readonly Dictionary<string, string> SupportedLanguages = new()
    {
        { AutoDetect, "Auto (System)" },
        { English, "English" },
        { NorwegianBokmal, "Norsk (Bokmål)" }
    };

    /// <summary>
    /// Initializes the application culture based on config or system default.
    /// Call this at application startup before any UI is created.
    /// </summary>
    public static void Initialize(string? configuredLanguage)
    {
        var culture = ResolveCulture(configuredLanguage);
        SetCulture(culture);
    }

    /// <summary>
    /// Resolves the culture to use based on configuration.
    /// </summary>
    public static CultureInfo ResolveCulture(string? configuredLanguage)
    {
        if (string.IsNullOrEmpty(configuredLanguage) || configuredLanguage == AutoDetect)
        {
            // Auto-detect from Windows settings
            var systemCulture = CultureInfo.CurrentUICulture;

            // Check if system culture is Norwegian (any variant)
            if (systemCulture.TwoLetterISOLanguageName is "nb" or "nn" or "no")
            {
                return new CultureInfo(NorwegianBokmal);
            }

            // Default to English
            return new CultureInfo(English);
        }

        // Use explicitly configured language
        try
        {
            return new CultureInfo(configuredLanguage);
        }
        catch
        {
            return new CultureInfo(English);
        }
    }

    /// <summary>
    /// Sets the culture for the current thread and the application.
    /// </summary>
    public static void SetCulture(CultureInfo culture)
    {
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        // For .NET 8, also set the default culture for new threads
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        // Update the resource manager culture
        Resources.Strings.Culture = culture;
    }

    /// <summary>
    /// Gets the current effective language code.
    /// </summary>
    public static string GetCurrentLanguageCode()
    {
        return Thread.CurrentThread.CurrentUICulture.Name switch
        {
            "nb-NO" or "nb" or "nn-NO" or "nn" => NorwegianBokmal,
            _ => English
        };
    }

    /// <summary>
    /// Gets the localized day abbreviation for a given day of week.
    /// </summary>
    public static string GetDayAbbreviation(DayOfWeek day)
    {
        return day switch
        {
            DayOfWeek.Monday => Resources.Strings.Day_Mon,
            DayOfWeek.Tuesday => Resources.Strings.Day_Tue,
            DayOfWeek.Wednesday => Resources.Strings.Day_Wed,
            DayOfWeek.Thursday => Resources.Strings.Day_Thu,
            DayOfWeek.Friday => Resources.Strings.Day_Fri,
            DayOfWeek.Saturday => Resources.Strings.Day_Sat,
            DayOfWeek.Sunday => Resources.Strings.Day_Sun,
            _ => day.ToString()[..3]
        };
    }

    /// <summary>
    /// Gets localized color name from the color key.
    /// </summary>
    public static string GetColorName(string colorKey)
    {
        return colorKey switch
        {
            "Yellow" => Resources.Strings.Color_Yellow,
            "Cyan" => Resources.Strings.Color_Cyan,
            "Lime" => Resources.Strings.Color_Lime,
            "Orange" => Resources.Strings.Color_Orange,
            "Magenta" => Resources.Strings.Color_Magenta,
            "White" => Resources.Strings.Color_White,
            "Red" => Resources.Strings.Color_Red,
            "Sky Blue" => Resources.Strings.Color_SkyBlue,
            _ => colorKey
        };
    }
}

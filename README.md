# EnturSystray

A Windows system tray application that displays real-time Norwegian public transit departure countdowns. See at a glance how many minutes until your next bus, tram, or metro arrives.

![.NET 8](https://img.shields.io/badge/.NET-8.0-blue)
![Windows](https://img.shields.io/badge/Platform-Windows-lightgrey)

## Features

- **Real-time departure countdowns** - Shows minutes until next departure directly in your taskbar
- **Multiple tray icons** - Configure up to 5 separate icons for different routes/stops
- **Flexible filtering** - Filter by stop, platform (quay), line number, or destination
- **Time-based schedules** - Show icons only during specific hours/days (e.g., commute times)
- **Customizable colors** - Choose icon text colors for easy identification
- **Bilingual** - English and Norwegian (Bokmål) interface

## Screenshot

The tray icon displays a countdown number (e.g., "12" = 12 minutes until departure). Hover for details on the next 3 departures.

## Requirements

- Windows 10/11
- .NET 8.0 Runtime

## Installation

### From Source

```bash
git clone https://github.com/oal/entur-systray.git
cd entur-systray
dotnet build
dotnet run
```

### Build Executable

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

## Usage

1. **First launch** - Right-click the tray icon and select "Settings"
2. **Add an icon** - Click "Add" to configure a new departure tracker
3. **Search for your stop** - Type a stop name to search Entur's database
4. **Filter departures** - Optionally select a specific platform, line, or destination
5. **Set a schedule** - Limit when the icon appears (e.g., weekday mornings only)
6. **Save** - The icon will immediately start showing countdown times

## Configuration

Settings are stored in `%APPDATA%\EnturSystray\config.json`

Each icon can be configured with:
- **Stop** - The bus stop/station (required)
- **Quay** - Specific platform (optional)
- **Line** - Filter to a specific line number (optional)
- **Destination** - Filter by destination text (optional)
- **Schedule** - Days and time range when the icon should appear

## Technologies

- **.NET 8.0** - Runtime and SDK
- **WPF** - Settings window UI
- **Windows Forms** - System tray (NotifyIcon)
- **Entur API** - Norwegian public transit data
  - GraphQL Journey Planner API for departures
  - Geocoder API for stop search
- **GDI+** - Dynamic icon generation

## API

This application uses the [Entur API](https://developer.entur.org/), Norway's national public transit data platform. No API key is required for basic usage.

## License

MIT

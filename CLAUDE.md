# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build the project
dotnet build

# Run the application
dotnet run
```

## Project Overview

EnturSystray is a Windows system tray application that displays real-time Norwegian public transit departure times. It uses the Entur GraphQL API to fetch bus/tram/metro departure information and displays countdown timers as taskbar tray icons.

**Target:** .NET 8.0 Windows (WPF + Windows Forms hybrid for system tray)

## Architecture

### Core Components

- **App.xaml.cs** - Application entry point managing the tray icon lifecycle. Uses two DispatcherTimers: one for API refresh (configurable interval), one for 1-second countdown updates. Manages multiple tray icons based on user configuration.

- **EnturService.cs** - HTTP client for Entur API. Uses GraphQL endpoint (`api.entur.io/journey-planner/v3/graphql`) for departures and Geocoder endpoint for stop search. Groups API calls by stop place to minimize requests when multiple icons share the same stop.

- **Config.cs** - Configuration stored in `%APPDATA%/EnturSystray/config.json`. Supports up to 5 tray icons, each with stop/quay/line/destination filters and time-based schedules. Includes migration logic from legacy single-icon format.

- **IconGenerator.cs** - Creates 32x32 tray icons using GDI+. Manages native icon handles via P/Invoke (`DestroyIcon`) to prevent resource leaks. Tracks handles per icon ID for proper cleanup.

- **TrayIconConfig** (in Config.cs) - Per-icon configuration including schedule (days of week, time range with overnight span support).

### Localization

Uses .NET resource files (`Resources/Strings.resx`, `Strings.nb-NO.resx`) with `LocalizationManager` for English/Norwegian support. Language can be auto-detected from Windows or manually configured. Language changes require application restart.

### Key Data Flow

1. User configures stops via `SettingsWindow` → `IconEditDialog`
2. `EnturService.GetDeparturesForAllIconsAsync()` batches requests by stop place
3. `App.UpdateAllDisplays()` recalculates minutes and updates each active icon
4. `IconGenerator.CreateBadgeIcon()` renders the countdown number

### Stop/Quay ID Format

- Stop places: `NSR:StopPlace:XXXXX`
- Quays (platforms): `NSR:Quay:XXXXX`

## Important Notes

- The `IconGenerator` uses P/Invoke to properly destroy icon handles - always call `Cleanup()` or `CleanupIcon()` to prevent GDI handle leaks
- NotifyIcon tooltip text has a 64-character limit (truncated automatically)
- Schedule time ranges support overnight spans (e.g., 22:00-06:00)

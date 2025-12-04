# Monitor Switcher

A simple Windows 11 app to quickly change your primary monitor in a multi-monitor setup.

## Features
- Lists all connected monitors with resolution and refresh rate
- One-click to set any monitor as primary
- Modern Windows 11 dark theme UI

## Requirements
- Windows 10/11
- .NET 8.0 SDK

## Build & Run

```bash
cd MonitorSwitcher
dotnet build
dotnet run
```

Or build a standalone executable:
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

## Usage
1. Launch the app
2. Click on any monitor card to set it as the primary display
3. The change takes effect immediately

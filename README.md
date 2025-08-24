# Daily Activity App

A modern Windows application for tracking daily activities with automatic reminders and Excel export capabilities.

## Features

- ✅ **24/7 Activity Tracking** - Automatic popup reminders at configurable intervals
- ✅ **Smart Type Management** - Remember and suggest activity types from history
- ✅ **Excel Export** - Export to Excel with 3 sheets: Detailed Log, Summary by Type, and Overtime tracking
- ✅ **Overtime Detection** - Automatically detect weekend work and after-hours activities
- ✅ **Dark Mode UI** - Modern dark theme with rounded panels and flat design
- ✅ **Flexible Popup Control** - "Don't show popup today" option with daily reset
- ✅ **Auto-Update** - Automatic update notifications from GitHub releases
- ✅ **History Management** - View, edit, and clear activity history
- ✅ **Lunch Reminders** - Special popup at lunch time (12:00-12:15)

## Installation

### Download Latest Release
1. Go to [Releases](https://github.com/Ad-Ins/DailyActivityApp/releases/latest)
2. Download `DailyActivityApp-vX.X.X.zip`
3. Extract and run `AdinersDailyActivityApp.exe` (single file, no installation needed)

### Build from Source
```bash
git clone https://github.com/Ad-Ins/DailyActivityApp.git
cd DailyActivityApp
dotnet build --configuration Release
dotnet run
```

## Usage

### Basic Usage
1. **First Run**: Application starts in system tray
2. **Activity Input**: Popup appears at configured intervals or double-click tray icon
3. **Type & Activity**: Enter activity type and description, press Enter to save
4. **History**: View grouped activities by date and type in the popup

### Tray Menu Options
- **Input Activity Now** - Manual activity input
- **Export Log to Excel** - Export activities to Excel file
- **Set Interval** - Configure popup reminder interval
- **Don't show popup today** - Disable popups until tomorrow
- **Check for Updates** - Manual update check
- **Clear History** - Delete all activity logs
- **Test Timer** - Debug timer status

### Excel Export
The export creates 3 worksheets:
1. **Detailed Log** - Date, Start, End, Duration, Type, Activity
2. **Summary by Type** - Type, Total Duration, Activity Count  
3. **Overtime** - Weekend and after-8PM activities with reasons

## Auto-Update System

The app automatically checks for updates daily and shows notifications when new versions are available.

### For Developers - Creating Releases

1. **Update Version** in `AdinersDailyActivityApp.csproj`:
   ```xml
   <Version>1.1.0</Version>
   <AssemblyVersion>1.1.0.0</AssemblyVersion>
   <FileVersion>1.1.0.0</FileVersion>
   ```

2. **Commit and Tag**:
   ```bash
   git add .
   git commit -m "Release v1.1.0"
   git tag v1.1.0
   git push origin main --tags
   ```

3. **GitHub Actions** will automatically:
   - Build the application
   - Create a ZIP file
   - Create a GitHub release
   - Users will get update notifications

## Configuration

Settings are stored in `config.json` in the application directory:
- `IntervalHours` - Popup reminder interval
- `DontShowPopupToday` - Temporary popup disable
- `CheckForUpdates` - Enable/disable update checks
- `LastUpdateCheck` - Last update check timestamp

## System Requirements

- Windows 10/11 (x64)
- No .NET Runtime required (self-contained)
- Internet connection (for updates)

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Changelog

### v1.4.0 (Latest)
- Single executable file (no .NET Runtime required)
- Self-contained deployment (~149MB)
- Portable application - no installation needed
- All previous features included

### v1.2.0
- 24/7 popup reminders (removed working hours restriction)
- "Don't show popup today" with daily auto-reset
- Dark mode message boxes for Test Timer and Clear History
- Auto-update system with GitHub integration
- Improved Excel export with 3 sheets (Detailed, Summary, Overtime)
- Enhanced overtime detection (weekends + after 8PM)

### v1.0.0 (Initial Release)
- Basic activity tracking
- Excel export functionality
- Dark mode UI
- Auto-update system
- 24/7 popup reminders
- Overtime detection


# Daily Activity App

A modern Windows application for tracking daily activities with automatic reminders and Excel export capabilities.

## Features

- ✅ **24/7 Activity Tracking** - Automatic popup reminders at configurable intervals
- ✅ **Smart Type Management** - Remember and suggest activity types from history
- ✅ **Excel Export** - Export to Excel with 3 sheets: Detailed Log, Summary by Type, and Overtime tracking
- ✅ **Overtime Detection** - Automatically detect weekend work and after-hours activities
- ✅ **Dark Mode UI** - Modern dark theme with rounded panels and flat design
- ✅ **Flexible Popup Control** - "Don't show popup today" option with daily reset
- ✅ **Auto-Update** - Automatic update notifications with auto-download from GitHub releases
- ✅ **History Management** - View, edit, and clear activity history with comprehensive edit dialog
- ✅ **Lunch Reminders** - Special popup at lunch time (12:00-12:15)
- ✅ **Windows Installer** - Professional installer with auto-start options and proper uninstall

## Installation

### Download Latest Release
1. Go to [Releases](https://github.com/Ad-Ins/DailyActivityApp/releases/latest)
2. Download `DailyActivityApp-Setup-vX.X.X.exe` (Windows Installer)
3. Run installer and follow setup wizard
4. Application will start automatically and appear in system tray

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
- **Check for Updates** - Manual update check with auto-download
- **Clear History** - Delete all activity logs
- **About** - Application information and features
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
   - Create Windows installer with Inno Setup
   - Create a GitHub release
   - Users will get update notifications with auto-download

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

### v1.9.2 (Latest)
- Enhanced edit dialog functionality with comprehensive error handling
- Improved timer information display (prioritizes next popup time)
- Fixed edit dialog bugs and validation issues
- Better error messages for troubleshooting

### v1.9.1
- Comprehensive edit functionality for activity history
- Edit dialog with start/end time modification
- Enhanced validation and error handling
- Improved user experience for history management

### v1.8.0
- Windows Installer with Inno Setup
- Professional installation experience
- Auto-start options and desktop shortcuts
- Publisher information (PT Adicipta Invosi Teknologi)
- Proper uninstall functionality

### v1.7.0
- Auto-download feature for updates
- Non-blocking update dialogs
- Progress bar for download status
- Enhanced update user experience

### v1.6.0
- About dialog with comprehensive app information
- Developer and company information display
- Feature overview in About section

### v1.4.0
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


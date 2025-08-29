# Daily Activity App

A modern Windows application for tracking daily activities with automatic reminders and Excel export capabilities.

## Features

- ✅ **Clockify-Style Timer** - Manual START/STOP button with real-time elapsed time display
- ✅ **Automatic Midnight Splitting** - Activities automatically split at 00:00 for accurate daily tracking
- ✅ **Exclude Time Periods** - Configure break times (lunch, coffee) that auto-pause/resume timer
- ✅ **On-the-Fly Editing** - Edit activity times and details with F2 key or right-click menu
- ✅ **Delete Activities** - Remove incorrect activities with F3 key or right-click menu
- ✅ **Smart Type Management** - Remember and suggest activity types from history
- ✅ **Excel Export** - Export to Excel with 3 sheets: Detailed Log, Summary by Type, and Overtime tracking
- ✅ **Overtime Detection** - Automatically detect weekend work and after-hours activities
- ✅ **Dark Mode UI** - Modern dark theme with rounded panels and flat design
- ✅ **Auto-Update** - Automatic update notifications with auto-download from GitHub releases
- ✅ **History Management** - View grouped activities by date and type with comprehensive management
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
2. **Start Timer**: Double-click tray icon, enter activity details, press Enter or click START
3. **Timer Running**: Real-time display shows elapsed time in tray and form title
4. **Stop Timer**: Click STOP button to save activity and reset timer
5. **History**: View grouped activities by date and type, edit with F2 key

### Tray Menu Options
- **Input Activity Now** - Manual timer start/activity input
- **Export Log to Excel** - Export activities to Excel file
- **Set Interval** - Configure popup reminder interval (legacy)
- **Exclude Times** - Configure break periods that auto-pause timer
- **Don't show popup today** - Disable popups until tomorrow
- **Check for Updates** - Manual update check with auto-download
- **About** - Application information and features
- **Timer Information** - Debug timer status

### Clockify-Style Timer
The app now uses a manual timer system similar to Clockify:
- **Enter Key**: Start timer with current activity details
- **START/STOP Button**: Toggle timer state (blue=START, red=STOP)
- **Real-time Display**: Shows elapsed time in tray icon and form title
- **Automatic Midnight Split**: Activities crossing midnight are automatically split into separate days

### Exclude Time Periods
Configure break times that automatically pause/resume the timer:
- **Setup**: Right-click tray → "Exclude Times" → Add periods (e.g., "Lunch: 12:00-13:00")
- **Auto-Pause**: Timer stops at break start, saves current activity
- **Auto-Resume**: Timer resumes after break with same activity
- **Multiple Periods**: Support for lunch, coffee breaks, meetings, etc.

### Activity Management
Edit and delete activities on-the-fly with comprehensive management capabilities:
- **F2 Key**: Quick edit selected activity in history
- **F3 Key**: Quick delete selected activity with confirmation
- **Right-click Menu**: Context menu with edit and delete options
- **Time-only Editing**: Modify start/end times while preserving dates
- **Safe Deletion**: Confirmation dialog prevents accidental deletions
- **Real-time Updates**: History refreshes automatically after edits or deletions

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

### v2.0.4 (Latest)
- **Delete Activity Feature**: F3 key and right-click menu to delete incorrect activities with confirmation
- **Streamlined Update Dialog**: Removed "Open GitHub" button for cleaner update experience
- **Enhanced Activity Management**: Complete edit and delete functionality for activity history

### v2.0.0
- **Clockify-Style Timer System**: Manual START/STOP button with real-time elapsed time display
- **Automatic Midnight Splitting**: Activities crossing midnight automatically split for accurate daily tracking
- **Exclude Time Periods**: Configure break times (lunch, coffee) that auto-pause/resume timer
- **Enhanced Activity Editing**: F2 key and right-click editing with time-only modification
- **Improved UI**: Taller About dialog and streamlined update notifications
- **Smart Timer Management**: Seamless activity continuity across breaks and midnight boundaries

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


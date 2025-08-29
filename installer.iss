; Script Inno Setup untuk Adiners Daily Activity App
[Setup]
AppName=Adiners Daily Activity
AppVersion={#GetVersionNumbersString("publish\AdinersDailyActivityApp.exe")}
AppPublisher=AdIns (Advance Innovations) - PT. Adicipta Inovasi Teknologi
AppPublisherURL=https://github.com/Ad-Ins/DailyActivityApp
AppSupportURL=https://github.com/Ad-Ins/DailyActivityApp/issues
AppUpdatesURL=https://github.com/Ad-Ins/DailyActivityApp/releases
DefaultDirName={autopf}\AdinersDailyActivity
DefaultGroupName=Adiners Daily Activity
OutputDir=installer-output
OutputBaseFilename=DailyActivityApp-Setup-v{#GetVersionNumbersString("publish\AdinersDailyActivityApp.exe")}
SetupIconFile=Assets\logo.ico
Compression=lzma
SolidCompression=yes
DisableProgramGroupPage=no
UninstallDisplayIcon={app}\AdinersDailyActivityApp.exe
PrivilegesRequired=lowest


[Files]
; File exe utama
Source: "publish\AdinersDailyActivityApp.exe"; DestDir: "{app}"; Flags: ignoreversion
; Semua DLL files
Source: "publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion
; Runtime files
Source: "publish\*.json"; DestDir: "{app}"; Flags: ignoreversion
; Folder Assets
Source: "Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Adiners Daily Activity"; Filename: "{app}\AdinersDailyActivityApp.exe"; IconFilename: "{app}\Assets\logo.ico"
Name: "{userdesktop}\Adiners Daily Activity"; Filename: "{app}\AdinersDailyActivityApp.exe"; IconFilename: "{app}\Assets\logo.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\AdinersDailyActivityApp.exe"; Description: "Launch Adiners Daily Activity"; Flags: nowait postinstall skipifsilent

[Registry]
; Force auto-start with 1 minute delay for Windows startup
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "AdinersDailyActivity"; \
    ValueData: "cmd /c timeout /t 60 /nobreak >nul && ""{app}\AdinersDailyActivityApp.exe""""; \
    Flags: uninsdeletevalue

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: checkedonce

[UninstallDelete]
Type: files; Name: "{app}\config.json"
Type: dirifempty; Name: "{app}"
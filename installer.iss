; Script Inno Setup untuk Adiners Daily Activity App
[Setup]
AppName=Adiners Daily Activity
AppVersion={#GetVersionNumbersString("publish\AdinersDailyActivityApp.exe")}
DefaultDirName={autopf}\AdinersDailyActivity
DefaultGroupName=Adiners Daily Activity
OutputDir=installer-output
OutputBaseFilename=DailyActivityApp-Setup-v{#GetVersionNumbersString("publish\AdinersDailyActivityApp.exe")}
SetupIconFile=Assets\logo.ico
Compression=lzma
SolidCompression=yes
DisableProgramGroupPage=no
UninstallDisplayIcon={app}\AdinersDailyActivityApp.exe
PrivilegesRequired=admin


[Files]
; File exe utama (single file)
Source: "publish\AdinersDailyActivityApp.exe"; DestDir: "{app}"; Flags: ignoreversion
; Folder Assets
Source: "Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Adiners Daily Activity"; Filename: "{app}\AdinersDailyActivityApp.exe"; IconFilename: "{app}\Assets\logo.ico"
Name: "{userdesktop}\Adiners Daily Activity"; Filename: "{app}\AdinersDailyActivityApp.exe"; IconFilename: "{app}\Assets\logo.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\AdinersDailyActivityApp.exe"; Description: "Launch Adiners Daily Activity"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "AdinersDailyActivity"; ValueData: """{app}\AdinersDailyActivityApp.exe"""; \
    Flags: uninsdeletevalue; Tasks: autostart

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: checkedonce
Name: "autostart"; Description: "Start with &Windows"; GroupDescription: "Startup options:"; Flags: checkedonce

[UninstallDelete]
Type: files; Name: "{app}\config.json"
Type: dirifempty; Name: "{app}"
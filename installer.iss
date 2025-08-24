; Script Inno Setup untuk Adiners Daily Activity App
[Setup]
AppName=Adiners Daily Activity
AppVersion=1.0
DefaultDirName={autopf}\AdinersDailyActivity
DefaultGroupName=Adiners Daily Activity
OutputDir=output
OutputBaseFilename=AdinersInstaller
SetupIconFile=Assets\logo.ico
Compression=lzma
SolidCompression=yes
DisableProgramGroupPage=no
UninstallDisplayIcon={app}\AdinersDailyActivityApp.exe


[Files]
; File exe utama
Source: "publish/AdinersDailyActivityApp.exe"; DestDir: "{app}"; Flags: ignoreversion
; File konfigurasi
Source: "publish/config.json"; DestDir: "{app}"; Flags: ignoreversion
; File DLL
Source: "publish/*.dll"; DestDir: "{app}"; Flags: ignoreversion

; Folder Assets
Source: "Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Adiners Daily Activity"; Filename: "{app}\AdinersDailyActivityApp.exe"; IconFilename: "{app}\Assets\logo.ico"
Name: "{userdesktop}\Adiners Daily Activity"; Filename: "{app}\AdinersDailyActivityApp.exe"; IconFilename: "{app}\Assets\logo.ico"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: checkedonce

[Run]
Filename: "{sys}\schtasks.exe"; \
    Parameters: "/Create /TN ""AdinersDailyActivity"" /TR """"{app}\AdinersDailyActivityApp.exe"""" /SC ONLOGON /RL HIGHEST /F"; \
    Flags: runhidden runascurrentuser

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "AdinersDailyActivity"; ValueData: """{app}\AdinersDailyActivityApp.exe"""; Flags: uninsdeletevalue

; Tambahan: Set app untuk selalu run as admin
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"; \
    ValueType: string; ValueName: "{app}\AdinersDailyActivityApp.exe"; ValueData: "RUNASADMIN"; \
    Flags: uninsdeletevalue

[UninstallRun]
Filename: "{sys}\schtasks.exe"; \
    Parameters: "/Delete /TN ""AdinersDailyActivity"" /F"; \
    Flags: runhidden runascurrentuser; \
    RunOnceId: "DeleteScheduledTask"
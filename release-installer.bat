@echo off
setlocal enabledelayedexpansion

echo ========================================
echo   Daily Activity App Installer Release
echo ========================================
echo.

if "%1"=="" (
    echo Usage: release-installer.bat [version] "[changelog]"
    echo Example: release-installer.bat 1.4.0 "Added installer support"
    echo.
    pause
    exit /b 1
)

if "%2"=="" (
    echo Usage: release-installer.bat [version] "[changelog]"
    echo Example: release-installer.bat 1.4.0 "Added installer support"
    echo.
    pause
    exit /b 1
)

set VERSION=%1
set CHANGELOG=%~2
echo Creating installer release for version %VERSION%...
echo Changelog: %CHANGELOG%
echo.

echo [1/8] Updating project file version...
powershell -Command "(Get-Content 'AdinersDailyActivityApp.csproj') -replace '<Version>.*</Version>', '<Version>%VERSION%</Version>' -replace '<AssemblyVersion>.*</AssemblyVersion>', '<AssemblyVersion>%VERSION%.0</AssemblyVersion>' -replace '<FileVersion>.*</FileVersion>', '<FileVersion>%VERSION%.0</FileVersion>' | Set-Content 'AdinersDailyActivityApp.csproj'"

echo [2/8] Updating README changelog...
powershell -Command "$content = Get-Content 'README.md' -Raw; $oldLatest = ($content | Select-String '### v([\d\.]+) \(Latest\)').Matches[0].Groups[1].Value; $newEntry = \"### v%VERSION% (Latest)`n- %CHANGELOG%`n`n### v$oldLatest\"; $content = $content -replace '### v[\d\.]+ \(Latest\)', $newEntry; Set-Content 'README.md' -Value $content"

echo [3/8] Building solution...
dotnet build -c Release
if errorlevel 1 (
    echo Build failed!
    pause
    exit /b 1
)

echo [4/8] Publishing executable...
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish
if errorlevel 1 (
    echo Publish failed!
    pause
    exit /b 1
)

echo [5/8] Testing executable...
if not exist "publish\AdinersDailyActivityApp.exe" (
    echo Executable not found!
    pause
    exit /b 1
)

echo [6/8] Building installer with Inno Setup...
if not exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    echo Inno Setup not found! Please install Inno Setup 6
    echo Download from: https://jrsoftware.org/isdl.php
    pause
    exit /b 1
)

"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
if errorlevel 1 (
    echo Installer build failed!
    pause
    exit /b 1
)

echo [7/8] Committing changes...
if exist publish rmdir /s /q publish
if exist installer-output\*.exe (
    echo Installer created successfully!
    dir installer-output\*.exe
) else (
    echo Installer not found!
    pause
    exit /b 1
)

git add .
git commit -m "Release v%VERSION% - %CHANGELOG%"

echo [8/8] Creating and pushing tag...
git tag v%VERSION%
git push origin main --tags

echo.
echo ========================================
echo Installer Release v%VERSION% completed!
echo.
echo Installer: 
dir installer-output\*.exe
echo.
echo GitHub Actions will now build and upload the installer.
echo ========================================
echo.
pause
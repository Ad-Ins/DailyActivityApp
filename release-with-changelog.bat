@echo off
setlocal enabledelayedexpansion

echo ========================================
echo    Daily Activity App Release Tool
echo ========================================
echo.

if "%1"=="" (
    echo Usage: release-with-changelog.bat [version] "[changelog]"
    echo Example: release-with-changelog.bat 1.4.0 "Added new features and bug fixes"
    echo.
    pause
    exit /b 1
)

if "%2"=="" (
    echo Usage: release-with-changelog.bat [version] "[changelog]"
    echo Example: release-with-changelog.bat 1.4.0 "Added new features and bug fixes"
    echo.
    pause
    exit /b 1
)

set VERSION=%1
set CHANGELOG=%~2
echo Creating release for version %VERSION%...
echo Changelog: %CHANGELOG%
echo.

echo [1/7] Updating project file version...
powershell -Command "(Get-Content 'AdinersDailyActivityApp.csproj') -replace '<Version>.*</Version>', '<Version>%VERSION%</Version>' -replace '<AssemblyVersion>.*</AssemblyVersion>', '<AssemblyVersion>%VERSION%.0</AssemblyVersion>' -replace '<FileVersion>.*</FileVersion>', '<FileVersion>%VERSION%.0</FileVersion>' | Set-Content 'AdinersDailyActivityApp.csproj'"

echo [2/7] Updating README changelog...
powershell -Command "$content = Get-Content 'README.md' -Raw; $oldLatest = ($content | Select-String '### v([\d\.]+) \(Latest\)').Matches[0].Groups[1].Value; $newEntry = \"### v%VERSION% (Latest)`n- %CHANGELOG%`n`n### v$oldLatest\"; $content = $content -replace '### v[\d\.]+ \(Latest\)', $newEntry; Set-Content 'README.md' -Value $content"

echo [3/7] Building solution...
dotnet build -c Release
if errorlevel 1 (
    echo Build failed!
    pause
    exit /b 1
)

echo [4/7] Publishing executable...
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish
if errorlevel 1 (
    echo Publish failed!
    pause
    exit /b 1
)

echo [5/7] Testing executable...
if not exist "publish\AdinersDailyActivityApp.exe" (
    echo Executable not found!
    pause
    exit /b 1
)

echo [6/7] Committing changes...
git add .
git commit -m "Release v%VERSION% - %CHANGELOG%"

echo [7/7] Creating and pushing tag...
git tag v%VERSION%
git push origin main --tags

echo.
echo ========================================
echo Release v%VERSION% completed successfully!
echo.
echo Executable: publish\AdinersDailyActivityApp.exe
echo Size: 
powershell -Command "'{0:N2} MB' -f ((Get-Item 'publish\AdinersDailyActivityApp.exe').Length / 1MB)"
echo.
echo GitHub Actions will now build and create the release.
echo Check: https://github.com/Ad-Ins/DailyActivityApp/releases
echo ========================================
echo.
pause
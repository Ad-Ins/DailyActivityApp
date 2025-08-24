@echo off
setlocal enabledelayedexpansion

echo ========================================
echo    Daily Activity App Release Tool
echo ========================================
echo.

if "%1"=="" (
    echo Usage: release.bat [version]
    echo Example: release.bat 1.4.0
    echo.
    pause
    exit /b 1
)

set VERSION=%1
echo Creating release for version %VERSION%...
echo.

echo [1/6] Updating project file version...
powershell -Command "(Get-Content 'AdinersDailyActivityApp.csproj') -replace '<Version>.*</Version>', '<Version>%VERSION%</Version>' -replace '<AssemblyVersion>.*</AssemblyVersion>', '<AssemblyVersion>%VERSION%.0</AssemblyVersion>' -replace '<FileVersion>.*</FileVersion>', '<FileVersion>%VERSION%.0</FileVersion>' | Set-Content 'AdinersDailyActivityApp.csproj'"

echo [2/6] Updating README changelog...
powershell -Command "$content = Get-Content 'README.md' -Raw; $content = $content -replace '### v[\d\.]+\s+\(Latest\)', '### v%VERSION% (Latest)'; Set-Content 'README.md' -Value $content"

echo [3/6] Building and publishing executable...
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish
if errorlevel 1 (
    echo Build failed!
    pause
    exit /b 1
)

echo [4/6] Testing executable...
if not exist "publish\AdinersDailyActivityApp.exe" (
    echo Executable not found!
    pause
    exit /b 1
)

echo [5/6] Committing changes...
git add .
git commit -m "Release v%VERSION% - Auto-generated release"

echo [6/6] Creating and pushing tag...
git tag v%VERSION%
git push origin main --tags

echo.
echo ========================================
echo Release v%VERSION% completed successfully!
echo.
echo Executable created: publish\AdinersDailyActivityApp.exe
echo GitHub Actions will now build and create the release.
echo ========================================
echo.
pause
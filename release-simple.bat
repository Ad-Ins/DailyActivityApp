@echo off
echo ========================================
echo   Daily Activity App Installer Release
echo ========================================
echo.

if "%1"=="" (
    echo Usage: release-simple.bat [version]
    echo Example: release-simple.bat 1.4.0
    echo.
    pause
    exit /b 1
)

set VERSION=%1
echo Creating installer release for version %VERSION%...
echo.

echo [1/7] Updating project file version...
powershell -Command "(Get-Content 'AdinersDailyActivityApp.csproj') -replace '<Version>.*</Version>', '<Version>%VERSION%</Version>' -replace '<AssemblyVersion>.*</AssemblyVersion>', '<AssemblyVersion>%VERSION%.0</AssemblyVersion>' -replace '<FileVersion>.*</FileVersion>', '<FileVersion>%VERSION%.0</FileVersion>' | Set-Content 'AdinersDailyActivityApp.csproj'"

echo [2/7] Building solution...
dotnet build -c Release
if errorlevel 1 (
    echo Build failed!
    pause
    exit /b 1
)

echo [3/7] Publishing executable...
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish
if errorlevel 1 (
    echo Publish failed!
    pause
    exit /b 1
)

echo [4/7] Testing executable...
if not exist "publish\AdinersDailyActivityApp.exe" (
    echo Executable not found!
    pause
    exit /b 1
)

echo [5/7] Building installer with Inno Setup...
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

echo [6/7] Committing changes...
if exist publish rmdir /s /q publish
git add .
git commit -m "Release v%VERSION% - Installer version"

echo [7/7] Creating and pushing tag...
git tag v%VERSION%
git push origin main --tags

echo.
echo ========================================
echo Installer Release v%VERSION% completed!
echo.
if exist installer-output\*.exe (
    echo Installer created:
    dir installer-output\*.exe
) else (
    echo No installer found in output directory
)
echo.
echo GitHub Actions will now build and upload the installer.
echo ========================================
echo.
pause
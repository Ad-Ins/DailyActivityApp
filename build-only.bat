@echo off
echo ========================================
echo    Building Daily Activity App
echo ========================================
echo.

echo [1/2] Building solution...
dotnet build -c Release
if errorlevel 1 (
    echo Build failed!
    pause
    exit /b 1
)

echo [2/2] Publishing executable...
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish
if errorlevel 1 (
    echo Publish failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo Build completed successfully!
echo.
echo Executable: publish\AdinersDailyActivityApp.exe
echo Size: 
powershell -Command "'{0:N2} MB' -f ((Get-Item 'publish\AdinersDailyActivityApp.exe').Length / 1MB)"
echo ========================================
echo.
pause
@echo off
setlocal
set PLUGIN=com.guacamole.razerbattery.sdPlugin
set DEST=%APPDATA%\Elgato\StreamDeck\Plugins\%PLUGIN%

echo [1/4] Building...
dotnet build "%~dp0RazerBattery.csproj" -c Release || goto :error

echo [2/4] Closing Stream Deck...
taskkill /f /im StreamDeck.exe >nul 2>&1
timeout /t 3 /nobreak >nul

echo [3/4] Copying plugin...
if exist "%DEST%" rmdir /s /q "%DEST%"
xcopy "%~dp0bin\Release\net48\*" "%DEST%\" /e /i /y >nul || goto :error

echo [4/4] Starting Stream Deck...
start "" "%ProgramFiles%\Elgato\StreamDeck\StreamDeck.exe"

echo.
echo Done. Find the "Peripheral Battery" category in the Stream Deck actions list.
pause
exit /b 0

:error
echo.
echo Something went wrong. Check the message above.
pause
exit /b 1

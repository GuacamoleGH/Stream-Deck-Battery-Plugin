@echo off
setlocal
set PLUGIN=com.jose.razerbattery.sdPlugin
set DEST=%APPDATA%\Elgato\StreamDeck\Plugins\%PLUGIN%
set LEGACY=%APPDATA%\Elgato\StreamDeck\Plugins\com.guacamole.razerbattery.sdPlugin

echo [1/4] Compilando...
dotnet build "%~dp0RazerBattery.csproj" -c Release || goto :error

echo [2/4] Cerrando Stream Deck...
taskkill /f /im StreamDeck.exe >nul 2>&1
taskkill /f /im RazerBattery.exe >nul 2>&1
timeout /t 3 /nobreak >nul

echo [3/4] Copiando el plugin...
if exist "%DEST%" rmdir /s /q "%DEST%"
xcopy "%~dp0bin\Release\net48\*" "%DEST%\" /e /i /y >nul || goto :error

if exist "%LEGACY%" (
    xcopy "%~dp0bin\Release\net48\*" "%LEGACY%\" /e /i /y >nul || goto :error
)

echo [4/4] Arrancando Stream Deck...
start "" "%ProgramFiles%\Elgato\StreamDeck\StreamDeck.exe"

echo.
echo Listo. Busca la categoria "Bateria perifericos" en la lista de acciones de Stream Deck.
pause
exit /b 0

:error
echo.
echo Algo ha fallado. Revisa el mensaje de arriba.
pause
exit /b 1

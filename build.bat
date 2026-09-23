@echo off
setlocal
cd /d "%~dp0"

echo ==========================================
echo       CFS NETWORK LAUNCHER v1.4.0 + AUTO UPDATE
echo ==========================================
echo.

if not exist "Assets\9462bc4b-a3f4-4fc9-8d15-a8416816ca90.ico" (
    echo HIBA: Az Assets\9462bc4b-a3f4-4fc9-8d15-a8416816ca90.ico hianyzik.
    pause
    exit /b 1
)

echo [0/4] Clean...
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"
if exist "publish" rmdir /s /q "publish"

echo.
echo [1/4] Restore...
dotnet restore
if errorlevel 1 goto :error

echo.
echo [2/4] Build...
dotnet build -c Release --no-restore
if errorlevel 1 goto :error

echo.
echo [3/4] Publish...
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "publish"
if errorlevel 1 goto :error

copy /Y "config.json" "publish\config.json" >nul

echo.
echo ==========================================
echo             BUILD SIKERES!
echo ==========================================
echo.
echo Inditsd innen:
echo %CD%\publish\CFSNetworkLauncher.exe
echo.
pause
exit /b 0

:error
echo.
echo ==========================================
echo             BUILD HIBA!
echo ==========================================
echo.
pause
exit /b 1

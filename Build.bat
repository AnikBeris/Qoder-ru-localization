@echo off
setlocal
cd /d "%~dp0"

set "QV="
for /f "usebackq delims=" %%v in ("QODER_VERSION.txt") do set "QV=%%v"
if not defined QV (
  echo QODER_VERSION.txt is missing or empty.
  exit /b 1
)

set "DICT=data\ru-translated-%QV%.json"
if not exist "%DICT%" (
  echo Dictionary for Qoder %QV% not found: %DICT%
  exit /b 1
)

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo .NET Framework 4.x compiler not found.
  exit /b 1
)

set "VERJSON=%TEMP%\qoder-ru-version-%QV%.json"
> "%VERJSON%" echo {"qoderVersion":"%QV%"}

"%CSC%" -nologo -target:winexe -platform:anycpu -optimize+ -codepage:65001 -r:System.Web.Extensions.dll -r:System.Windows.Forms.dll -r:System.Drawing.dll -resource:"%DICT%",qoder-ru-dict.json -resource:"data\ru-extra-ui.json",qoder-ru-extra.json -resource:"data\ru-overlay.json",qoder-ru-overlay.json -resource:"data\ru-knowledge.json",qoder-ru-knowledge.json -resource:"%VERJSON%",qoder-ru-version.json -out:"QoderRU-Setup-%QV%.exe" install\QoderRuSetup.cs
if errorlevel 1 (
  echo.
  echo Build failed.
  exit /b 1
)

echo.
echo Done: QoderRU-Setup-%QV%.exe

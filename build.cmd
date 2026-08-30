@echo off
rem CutGeek local build. Mirrors .github\workflows\release.yml so a local artefact and a
rem CI artefact are built the same way.
rem
rem   build.cmd            - build and run the checks
rem   build.cmd publish    - also produce the portable single-file executable
rem   build.cmd installer  - also compile the installer with Inno Setup

setlocal
cd /d "%~dp0"

dotnet build CutGeek.sln -c Release || exit /b 1
dotnet run --project tests\CutGeek.Tests -c Release --no-build || exit /b 1

if /i "%~1"=="" goto :eof

dotnet publish src\CutGeek\CutGeek.csproj -c Release -r win-x64 ^
  --self-contained true -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true -o publish\app || exit /b 1

if /i not "%~1"=="installer" goto :eof

set ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe
if not exist "%ISCC%" (
  echo Inno Setup 6 was not found at "%ISCC%".
  exit /b 1
)
"%ISCC%" installer\CutGeek.iss || exit /b 1

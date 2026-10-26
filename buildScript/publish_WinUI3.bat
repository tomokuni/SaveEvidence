@echo off
setlocal
chcp 65001 >nul

rem ============================================================
rem  SaveEvidence (WinUI 3) publish script
rem
rem  ASCII only.  cmd.exe reads a batch file with the console code page and counts the read
rem  position in decoded characters, so non-ASCII text garbles the output and can make cmd
rem  execute fragments of the file as commands.  The chcp below keeps the UTF-8 log of
rem  dotnet readable (safe because this file is ASCII).  Rules: .github/RELEASE.md.
rem
rem  This script publishes the single file shape (the default).  The AOT shape lives in
rem  buildScript/publish_WinUI3Aot.bat.  The mode is chosen by buildScript/publish_ui.bat.
rem  Measured 2026-10-20: the single file distribution starts and exits with code 0.
rem
rem  Single file publishing needs the WinUI 3 libraries to be built with a RID first:
rem  single file publishing requires the .pri (resource index) of every WinUI 3 project as an
rem  input (PRI175 / PRI252), but RuntimeIdentifier does not flow to ProjectReference, so the
rem  .pri files of the referenced projects land in the TFM only folder and not in the
rem  <TFM>\<RID>\ folder that the app looks in.  Building the libraries directly with -r puts
rem  the .pri files in <TFM>\<RID>\ (the libraries do not need EnableMsixTooling; measured).
rem
rem  Layout: unpackaged (WindowsPackageType=None) and self-contained Windows App SDK
rem  (WindowsAppSDKSelfContained), so the Windows App SDK runtime is shipped.  The main
rem  project needs EnableMsixTooling for publish (without it the publish does not produce
rem  SaveEvidence.WinUI3.pri and the app starts but fails on exit with 0xC000027B /
rem  0x80008083, a WinRT stowed exception; measured).
rem
rem  Settings JSON: the app injects the JSON source generation context (SettingsJsonContext)
rem  into SettingsService.  The context must live in the assembly that references Settings
rem  (in one compilation the JSON source generator does not see the MVVM generator output).
rem  The round trip of the settings is verified by a regression test
rem  (src\App.WinUI3\tests\AppJsonContext.Tests.cs), which runs with the tests of
rem  buildScript/verify.ps1.
rem ============================================================

rem Move from the script folder (buildScript) to the repository root.
pushd "%~dp0.."

rem The target runtime (default: win-x64).
if "%~1"=="" (set "RID=win-x64") else (set "RID=%~1")

rem  Generate the .pri files of the WinUI 3 libraries (RID: %RID%).
rem  Single file publishing requires the .pri (resource index) of the WinUI 3 libraries as an
rem  input, but RuntimeIdentifier does not flow to ProjectReference, so building only the app
rem  leaves the .pri files in the TFM only folder.  Building the library directly with -r
rem  produces them in <TFM>\<RID>\ (measured 2026-10-16).
rem  Only the libraries of this repository are built here.  EsUtil is a package reference and the
rem  packages carry their .pri; building the EsUtil projects here became wrong after the switch
rem  to packages (the folder does not exist in CI, and the app then exited with 0xC0000409).
rem  Measured 2026-10-26.
rem  A failure here does not stop the script (a mode that does not need .pri files can still
rem  succeed).
echo [INFO] WinUI 3: generating the .pri files of the libraries (RID: %RID%)
for %%p in (
  "src\Parts.CapturePreview.ControlWinUI3\src\Parts.CapturePreview.ControlWinUI3.csproj"
) do dotnet build %%p -c Release -r %RID% -v q --nologo -p:NuGetLockFilePath=packages.%RID%.lock.json >nul 2>&1

rem The default shape is one self contained file.  Native AOT cannot bundle native libraries
rem (measured 2026-10-20: PublishSingleFile + IncludeNativeLibrariesForSelfExtract together with
rem PublishAot still leaves the native DLLs next to the exe), and Windows App SDK rejects single
rem file publishing while it is framework dependent, so "one file and no prerequisite" means a
rem self contained single file publish without AOT.  The AOT shape lives in
rem buildScript/publish_WinUI3Aot.bat (a folder of native DLLs, also without a prerequisite).
rem Single file publishing is gated by `SaveEvidencePublishSingleFile` in
rem src\App.WinUI3\src\App.WinUI3.csproj: passing `-p:PublishSingleFile=true` directly flows to
rem the referenced projects and the WinUI 3 libraries then fail (measured).
set "SE_MODES=single"
set "SE_SINGLE_ARGS=-p:SaveEvidencePublishSingleFile=true"

call "%~dp0publish_ui.bat" winui3 "src\App.WinUI3\src\App.WinUI3.csproj" %*
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%

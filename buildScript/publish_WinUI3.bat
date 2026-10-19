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
rem  The mode (aot ^> single ^> exe) is chosen by buildScript/publish_ui.bat.
rem  Measured 2026-10-16: mode 1 (single + AOT) cannot be used.  Widening the requirements of
rem  the CsWinRT AOT analysis (CsWinRT1028 / CsWinRT1030: make the types that cross the WinRT
rem  ABI partial, enable AllowUnsafeBlocks) to the app and to the WinUI 3 control projects
rem  does not fix it.  Native AOT of WinUI 3 (C#) is not supported as of Windows App SDK 1.8,
rem  and the XAML runtime depends on reflection, so avoiding the analysis warnings does not
rem  make it work either.  Mode 2 (single) is used instead.
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
rem  Single file publishing requires the .pri (resource index) of every WinUI 3 project as an
rem  input, but RuntimeIdentifier does not flow to ProjectReference, so building only the app
rem  leaves the .pri files in the TFM only folder.  Building the libraries directly with -r
rem  produces them in <TFM>\<RID>\ (measured 2026-10-16).
rem  A failure here does not stop the script (a mode that does not need .pri files can still
rem  succeed).
echo [INFO] WinUI 3: generating the .pri files of the libraries (RID: %RID%)
for %%p in (
  "..\EsUtil\src\EsUtil.Feature.Loupe.ControlWinUI3\src\WinUI3\EsUtil.Feature.Loupe.ControlWinUI3.csproj"
  "..\EsUtil\src\EsUtil.Feature.Marquee.ControlWinUI3\src\WinUI3\EsUtil.Feature.Marquee.ControlWinUI3.csproj"
  "src\Platform.WinUI3\src\Platform.WinUI3.csproj"
  "src\Parts.CapturePreview.ControlWinUI3\src\Parts.CapturePreview.ControlWinUI3.csproj"
) do dotnet build %%p -c Release -r %RID% -v q --nologo >nul 2>&1

rem Single file publishing is gated by `SaveEvidencePublishSingleFile` in
rem src\App.WinUI3\src\App.WinUI3.csproj.  Passing `-p:PublishSingleFile=true` directly
rem flows to the referenced projects, and the WinUI 3 libraries then fail (measured).
set "SE_SINGLE_ARGS=-p:SaveEvidencePublishSingleFile=true"

call "%~dp0publish_ui.bat" winui3 "src\App.WinUI3\src\App.WinUI3.csproj" %*
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%

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
rem  This script publishes **two shapes** for this UI (both are release assets):
rem    - Native AOT in a folder with a framework-dependent Windows App SDK
rem    - one self-contained exe with the Windows App Runtime and .NET bundled
rem  The mode of each publish is chosen by buildScript/publish_ui.bat.
rem  Measured 2026-11-05: the AOT folder starts and exits with code 0 (verified by
rem  .github/verify/verifyPublish.ps1).  Measured 2026-11-10: the single self-contained exe
rem  (293.5 MB) starts, draws and exits with code 0.
rem
rem  Single file publishing needs the WinUI 3 libraries to be built with a RID first:
rem  single file publishing requires the .pri (resource index) of every WinUI 3 project as an
rem  input (PRI175 / PRI252), but RuntimeIdentifier does not flow to ProjectReference, so the
rem  .pri files of the referenced projects land in the TFM only folder and not in the
rem  <TFM>\<RID>\ folder that the app looks in.  Building the libraries directly with -r puts
rem  the .pri files in <TFM>\<RID>\ (the libraries do not need EnableMsixTooling; measured).
rem
rem  Layout: unpackaged (WindowsPackageType=None) and framework-dependent Windows App SDK
rem  (WindowsAppSDKSelfContained=false), so the Windows App Runtime installed on the machine is
rem  used (code pages are shared: measured 2026-11-05, smaller and lower memory than the former
rem  self-contained shape).  The app checks for the runtime at startup and, when it is missing,
rem  asks the user and installs it through EsUtil.Ui.WinUI3.Interop
rem  (Bootstrapping.WindowsAppRuntimeBootstrap, published in EsUtil 0.5.2).  The SDK auto
rem  initialization is turned off (WindowsAppSdkBootstrapInitialize=false) so that only that
rem  implementation initializes the runtime.
rem  The main project needs EnableMsixTooling for publish (without it the publish does not produce
rem  SaveEvidence.WinUI3.pri and the app starts but fails on exit with 0xC000027B /
rem  0x80008083, a WinRT stowed exception; measured).
rem
rem  Settings JSON: the app injects the JSON source generation context (SettingsJsonContext)
rem  into SettingsService.  The context must live in the assembly that references Settings
rem  (in one compilation the JSON source generator does not see the MVVM generator output).
rem  The round trip of the settings is verified by a regression test
rem  (src\App.WinUI3\tests\AppJsonContext.Tests.cs), which runs with the tests of
rem  .github/verify/verify.ps1.
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

rem  Show the lower bound of the Windows App Runtime that the AOT shape needs.
rem  The value is owned by Directory.Packages.props (Microsoft.WindowsAppSDK); the app asks for the
rem  same version at start (WindowsAppRuntimeRequirement).  Keep it in sync, do not repeat the number
rem  in this script.
set "WASDK="
for /f "tokens=3 delims==" %%v in ('findstr /c:"Include=\"Microsoft.WindowsAppSDK\"" Directory.Packages.props') do if not defined WASDK set "WASDK=%%v"
if defined WASDK set "WASDK=%WASDK:"=%"
for /f "tokens=1" %%v in ("%WASDK%") do set "WASDK=%%v"
if defined WASDK echo [INFO] Windows App Runtime: %WASDK% or later (x64) is required for the AOT shape
if not defined WASDK echo [WARN] Windows App Runtime: the lower bound was not found in Directory.Packages.props

rem  The UI ships **two shapes** and both are release assets:
rem    1. aot     Native AOT in a folder (framework-dependent Windows App SDK).  Smallest and
rem               lowest memory when the runtime is installed (code pages are shared).
rem    2. single  One self-contained exe (Windows App Runtime AND .NET bundled, files extracted at
rem               start).  Runs on a machine where nothing can be installed (Windows Sandbox, a
rem               fresh VM).  Measured 2026-11-10: 293.5 MB, starts, draws and exits with code 0.
rem  The folder name carries the shape (publish\winui3-win-x64-aot / winui3-win-x64-single).
rem  The asset name carries it too (SaveEvidence.WinUI3.{version}.zip / ...WinUI3.Single.{version}.zip);
rem  the rule is owned by .github (naming rules document).
rem  The app checks for the Windows App Runtime at startup and installs it when the user agrees
rem  (EsUtil.Ui.WinUI3.Interop / Bootstrapping.WindowsAppRuntimeBootstrap, published in EsUtil 0.5.2).
rem  The single shape needs the runtime directory to be set before program entry; Program.Main does it
rem  when it detects a single file bundle (measured 2026-11-10).

rem ---- Shape 1: Native AOT in a folder (framework-dependent Windows App SDK) ----
rem  AOT is enabled by `SaveEvidencePublishAot` in src\App.WinUI3\src\App.WinUI3.csproj:
rem  passing `-p:PublishAot=true` directly flows to the referenced projects (measured).
set "SE_MODES=aot"
set "SE_AOT_ARGS=-p:SaveEvidencePublishAot=true"
set "SE_SINGLE_ARGS="
call "%~dp0publish_ui.bat" winui3 "src\App.WinUI3\src\App.WinUI3.csproj" %*
set "RESULT_AOT=%ERRORLEVEL%"
set "SE_AOT_ARGS="

rem ---- Shape 2: one self-contained exe (Windows App Runtime + .NET bundled) ----
rem  `SaveEvidenceSelfContained` bundles both runtimes (see src\App.WinUI3\src\App.WinUI3.csproj).
rem  `IncludeNativeLibrariesForSelfExtract` keeps the native Windows App Runtime libraries inside the
rem  exe and extracts them at start (required by the Windows App SDK single file support).
set "SE_MODES=single"
set "SE_SINGLE_ARGS=-p:SaveEvidenceSelfContained=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true"
call "%~dp0publish_ui.bat" winui3 "src\App.WinUI3\src\App.WinUI3.csproj" %*
set "RESULT_SINGLE=%ERRORLEVEL%"

popd
rem Both shapes are release assets, so a failure in either one fails the script.
if not "%RESULT_AOT%"=="0" exit /b %RESULT_AOT%
exit /b %RESULT_SINGLE%

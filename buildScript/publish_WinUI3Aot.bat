@echo off
setlocal
chcp 65001 >nul

rem ============================================================
rem  SaveEvidence (WinUI 3) publish script - Native AOT shape
rem
rem  ASCII only (rules: .github/RELEASE.md).
rem
rem  Why a separate script: the default shape (buildScript/publish_WinUI3.bat) is one self
rem  contained file.  Native AOT cannot produce that shape, because it cannot bundle native
rem  libraries (measured 2026-10-20: PublishSingleFile and IncludeNativeLibrariesForSelfExtract
rem  together with PublishAot still leave the native DLLs next to the exe).  This script
rem  publishes the AOT shape instead: one exe plus the native libraries it needs, with the
rem  Windows App SDK shipped alongside (WindowsAppSDKSelfContained), so no prerequisite is
rem  required on the target machine either.
rem
rem  Measured 2026-10-20: CsWinRT1030 for List<ToggleButton> was removed by storing the control
rem  references in arrays.  Native code generation reports two IL2026 warnings from generated
rem  SkiaSharp WinRT activation-factory code; keep them visible and exempt only IL2026 from
rem  warnings-as-errors.  The AOT folder distribution starts and exits with code 0.
rem
rem  Publishing needs the .pri files of the WinUI 3 libraries to be built with a RID first
rem  (the same preparation as the single file script explains).  Only the libraries of this
rem  repository are built: EsUtil is a package reference and the packages carry their .pri.
rem ============================================================

pushd "%~dp0.."

rem The target runtime (default: win-x64).
if "%~1"=="" (set "RID=win-x64") else (set "RID=%~1")

echo [INFO] WinUI 3: generating the .pri files of the libraries (RID: %RID%)
for %%p in (
  "src\Parts.CapturePreview.ControlWinUI3\src\Parts.CapturePreview.ControlWinUI3.csproj"
) do dotnet build %%p -c Release -r %RID% -v q --nologo -p:NuGetLockFilePath=packages.%RID%.lock.json >nul 2>&1

rem WindowsAppSDKSelfContained stays true (the csproj default): a framework dependent AOT build
rem would be much smaller but would require the Windows App SDK runtime on the target machine.
rem Single file publishing is not combined with AOT (it cannot bundle native libraries).
set "SE_MODES=aot"
set "SE_AOT_ARGS=-p:PublishAot=true"

call "%~dp0publish_ui.bat" winui3 "src\App.WinUI3\src\App.WinUI3.csproj" %*
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%

@echo off
setlocal
chcp 65001 >nul

rem ============================================================
rem  SaveEvidence (WinForms) publish script
rem
rem  ASCII only.  cmd.exe reads a batch file with the console code page and counts the read
rem  position in decoded characters, so non-ASCII text garbles the output and can make cmd
rem  execute fragments of the file as commands.  The chcp below keeps the UTF-8 log of
rem  dotnet readable (safe because this file is ASCII).  Rules: .github/RELEASE.md.
rem
rem  The mode (aot ^> single ^> exe) is chosen by buildScript/publish_ui.bat.
rem  Measured 2026-10-16: mode 1 (single + AOT) cannot be used.  NETSDK1175: trimming is not
rem  supported or recommended for Windows Forms.  Native AOT requires trimming, so it cannot
rem  be used for WinForms (the SDK rejects it explicitly because data binding and resources
rem  break under trimming).  Mode 2 (single) is used instead: the distribution is one exe,
rem  and the native libraries are extracted to a temporary folder at startup
rem  (IncludeNativeLibrariesForSelfExtract).
rem
rem  Prerequisite: Windows Forms (System.Drawing.Common) is Windows only.
rem
rem  Settings JSON: the app injects the JSON source generation context (SettingsJsonContext)
rem  into SettingsService (for trimming safety and startup time).  The context must live in
rem  the assembly that references Settings (in one compilation the JSON source generator does
rem  not see the MVVM generator output).  The round trip of the settings is verified with the
rem  real exe by buildScript/verifyPublish.ps1 (this project has no dedicated unit test for
rem  it).
rem ============================================================

rem Move from the script folder (buildScript) to the repository root.
pushd "%~dp0.."
call "%~dp0publish_ui.bat" winforms "src\App.WinForms\src\App.WinForms.csproj" %*
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%

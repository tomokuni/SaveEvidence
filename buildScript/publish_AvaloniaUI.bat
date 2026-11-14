@echo off
setlocal
chcp 65001 >nul

rem ============================================================
rem  SaveEvidence (Avalonia UI) publish script
rem
rem  ASCII only.  cmd.exe reads a batch file with the console code page and counts the read
rem  position in decoded characters, so non-ASCII text garbles the output and can make cmd
rem  execute fragments of the file as commands.  The chcp below keeps the UTF-8 log of
rem  dotnet readable (safe because this file is ASCII).  Rules: .github/RELEASE.md.
rem
rem  The mode is restricted to aot (see the SE_MODES line below).
rem  FluentAvaloniaUI (the Fluent theme, used for the confirmation dialog) references
rem  Avalonia.Controls.DataGrid 12.1.x, whose TypeHelper.GetPropertyOrIndexer produces IL2070 /
rem  IL3050 (reported as the assembly summaries IL2104 / IL3053).  The publish runs with
rem  -warnaserror, so AOT failed while the theme was in use (measured 2026-11-13: the same
rem  failure with DataGrid 12.1.0 and 12.1.2).  The app does not use DataGrid, so the two IDs
rem  are ignored for the AOT publish only (IlcArg in
rem  src\App.AvaloniaUI\src\App.AvaloniaUI.csproj; the list of allowed suppressions is owned by
rem  the build settings rules in .github, Appendix A).  Every other warning still fails the
rem  publish.
rem  Measured 2026-11-14: the AOT publish succeeds and produces one exe (34.8 MB) next to the
rem  native DLLs.
rem
rem  Prerequisite: linking for Native AOT needs link.exe from the Visual Studio C++ build
rem  tools.
rem
rem  Layout: all managed code is compiled into one exe.  The native libraries of Avalonia,
rem  SkiaSharp and OpenCvSharp cannot be embedded into the exe, so they are distributed next
rem  to it.
rem
rem  Does not work with AOT (measured): resolving XAML at run time
rem  (AvaloniaXamlLoader.Load(Uri)) fails with "No precompiled XAML found".  XAML must be
rem  taken in at compile time (Styles with x:Class).
rem
rem  Settings JSON: reflection based System.Text.Json does not work with Native AOT (the
rem  public properties of Settings are trimmed away and the settings file becomes {} on
rem  exit, losing everything).  The app therefore injects the JSON source generation context
rem  (SettingsJsonContext) into SettingsService.  The context must live in the assembly that
rem  references Settings (in one compilation the JSON source generator does not see the MVVM
rem  generator output).  The round trip of the settings is verified by a regression test
rem  (src\App.AvaloniaUI\tests\SettingsJsonContext.Tests.cs), which runs with the tests of
rem  .github/verify/verify.ps1.
rem ============================================================

rem Move from the script folder (buildScript) to the repository root.
pushd "%~dp0.."

rem Native AOT in a folder (managed code compiled to native; the native libraries of Avalonia,
rem SkiaSharp and OpenCvSharp are distributed next to the exe).
rem The AOT flag is gated by SaveEvidencePublishAot in src\App.AvaloniaUI\src\App.AvaloniaUI.csproj:
rem passing -p:PublishAot=true directly flows to the referenced projects (measured 2026-10-16).
set "SE_MODES=aot"
set "SE_AOT_ARGS=-p:SaveEvidencePublishAot=true -p:PublishSingleFile=true"

call "%~dp0publish_ui.bat" avaloniaui "src\App.AvaloniaUI\src\App.AvaloniaUI.csproj" %*
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%

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
rem  The mode is restricted to single (see the SE_MODES line below).
rem  AOT worked until 0.1.5.  It now fails because FluentAvaloniaUI (the Fluent theme,
rem  used for the confirmation dialog) references Avalonia.Controls.DataGrid 12.1.x, whose
rem  TypeHelper.GetPropertyOrIndexer produces IL2070 / IL3050 (reported as the assembly
rem  summaries IL2104 / IL3053).  The publish runs with -warnaserror, so AOT cannot be used
rem  (measured 2026-11-13: the same failure with DataGrid 12.1.0 and 12.1.2).
rem  To go back to AOT, remove this restriction after the DataGrid dependency is gone or
rem  annotated, and update uis[].output in .github/release-config.json (.github/RELEASE.md).
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

rem Single only (reason above).  publish_ui.bat falls back to exe if single fails.
set "SE_MODES=single"

call "%~dp0publish_ui.bat" avaloniaui "src\App.AvaloniaUI\src\App.AvaloniaUI.csproj" %*
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%

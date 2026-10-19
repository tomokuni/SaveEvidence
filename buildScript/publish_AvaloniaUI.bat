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
rem  The mode (aot ^> single ^> exe) is chosen by buildScript/publish_ui.bat.
rem  Measured 2026-10-16: mode 1 (single + AOT) is used.  Mode 1 failed before that because
rem  EsUtil.Feature.Loupe.ControlAvaloniaUI resolved the display adapter contract with
rem  Expression.Compile and Type.GetMethod (IL2070 / IL3050); removing the reflection fixed
rem  it.  If mode 1 stops working, first check whether reflection came back in EsUtil (the
rem  public contract is: the source image is received as a core image, and the projection
rem  for display is passed as an argument).
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
rem  buildScript/verify.ps1.
rem ============================================================

rem Move from the script folder (buildScript) to the repository root.
pushd "%~dp0.."
call "%~dp0publish_ui.bat" avaloniaui "src\App.AvaloniaUI\src\App.AvaloniaUI.csproj" %*
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%

@echo off
setlocal

rem ============================================================
rem  SaveEvidence (Avalonia UI) ビルドスクリプト
rem  Release / Native AOT（単一 exe + ネイティブ DLL）
rem
rem  方式: PublishAot=true（自己完結・単一 exe）
rem
rem  前提:
rem    Native AOT のリンクには Visual Studio の C++ ビルドツール（link.exe）が必要です。
rem
rem  構成（実測）:
rem    - マネージドコードは全て App.AvaloniaUI.exe（約 27.9 MB）へコンパイルされます。
rem    - Avalonia / SkiaSharp / OpenCvSharp のネイティブライブラリは exe へ同梱できないため、
rem      同じフォルダーへ並べて配布します（libSkiaSharp.dll / av_libglesv2.dll /
rem      libHarfBuzzSharp.dll / OpenCvSharpExtern.dll）。
rem
rem  方式の選定理由:
rem    - 単一 exe（PublishSingleFile + 自己完結）は exe が約 229 MB になるため採用していません。
rem    - AOT はマネージドコードのみをネイティブへコンパイルするため約 27.9 MB になります。
rem
rem  AOT で動作しない実装（実測）:
rem    - 実行時の XAML 解決（AvaloniaXamlLoader.Load(Uri)）は
rem      「No precompiled XAML found」で失敗します。XAML はコンパイル時に取り込む形
rem      （x:Class 付きの Styles）にしてください（ColorViewThemeManager が該当しました）。
rem
rem  設定 JSON（重要）:
rem    Native AOT ではリフレクションベースの System.Text.Json が動作しません
rem    （Settings の公開プロパティがトリミングで削除され、終了時に設定が空 {} になり全損します）。
rem    そのためアプリ本体は JSON ソース生成コンテキスト（SettingsJsonContext）を
rem    SettingsService へ注入しています。コンテキストは Settings を参照する側の
rem    アセンブリに置く必要があります（同一コンパイル内では MVVM のソース生成結果を
rem    JSON のソース生成が見られないため）。
rem    設定の往復は回帰テスト（src/App.AvaloniaUI/tests/SettingsJsonContext.Tests.cs）が
rem    検証します（buildScript/verify.ps1 のテスト実行に含まれます）。
rem ============================================================

rem スクリプトの場所（buildScript）からリポジトリルートへ移動する
pushd "%~dp0.."

rem ターゲットランタイム（既定: win-x64）
if "%~1"=="" (set "RID=win-x64") else (set "RID=%~1")

set "APP_NAME=App.AvaloniaUI.exe"

rem 最終的な出力先（MewUI 版と分ける）
set "OUTDIR=%CD%\publish\avaloniaui-%RID%-aot"

rem 発行の一時ステージング先
rem リポジトリ内へ直接発行すると、参照プロジェクト側にも publish が作られる。
rem そのためリポジトリ外の一時領域へ発行し、必要なファイルのみを出力先へ配置する。
set "STAGE=%TEMP%\saveevidence-aot-avalonia-%RID%"

echo.
echo [INFO] Mode  : Native AOT (single exe)
echo [INFO] Target: %RID%
echo [INFO] Output: %OUTDIR%
echo.

if exist "%STAGE%" rd /s /q "%STAGE%"

dotnet publish "src\App.AvaloniaUI\src\App.AvaloniaUI.csproj" ^
  -c Release ^
  -r "%RID%" ^
  -p:PublishAot=true ^
  -p:SelfContained=true ^
  -o "%STAGE%"

if errorlevel 1 (
  echo.
  echo [ERROR] publish failed. See log above.
  if exist "%STAGE%" rd /s /q "%STAGE%"
  popd
  exit /b 1
)

if not exist "%STAGE%\%APP_NAME%" (
  echo.
  echo [ERROR] Executable not found: %STAGE%\%APP_NAME%
  if exist "%STAGE%" rd /s /q "%STAGE%"
  popd
  exit /b 1
)

rem 出力先を作り直し、exe とネイティブ DLL のみを配置する（.pdb は配布対象外）
if exist "%OUTDIR%" rd /s /q "%OUTDIR%"
mkdir "%OUTDIR%"
copy /y "%STAGE%\*.exe" "%OUTDIR%\" >nul
if exist "%STAGE%\*.dll" copy /y "%STAGE%\*.dll" "%OUTDIR%\" >nul
rd /s /q "%STAGE%"

echo.
echo [OK] Built: %OUTDIR%\%APP_NAME%
popd
endlocal

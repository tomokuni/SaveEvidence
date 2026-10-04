@echo off
setlocal

rem ============================================================
rem  SaveEvidence (MewUI) ビルドスクリプト
rem  Release / Native AOT（単一 exe + ネイティブ DLL）
rem
rem  方式: PublishAot=true（自己完結・単一 exe）
rem
rem  前提:
rem    Native AOT のリンクには Visual Studio の C++ ビルドツール（link.exe）が必要です。
rem
rem  構成:
rem    - マネージドコードは全て exe（SaveEvidence.MewUI.exe）へコンパイルされます。
rem    - SkiaSharp / OpenCvSharp のネイティブライブラリは exe へ同梱できないため、
rem      同じフォルダーへ並べて配布します（libSkiaSharp.dll / OpenCvSharpExtern.dll）。
rem      サイズ・ファイル数はコードの変更で変わるため、ここには書きません（publish の出力で確認します）。
rem
rem  設定 JSON（重要）:
rem    Native AOT ではリフレクションベースの System.Text.Json が動作しません
rem    （Settings の公開プロパティがトリミングで削除され、設定が空 {} になる）。
rem    そのためアプリ本体は JSON ソース生成コンテキスト（SettingsJsonContext）を
rem    SettingsService へ注入しています。コンテキストは Settings を参照する側の
rem    アセンブリに置く必要があります（同一コンパイル内では MVVM のソース生成結果を
rem    JSON のソース生成が見られないため）。
rem ============================================================

rem スクリプトの場所（buildScript）からリポジトリルートへ移動する
pushd "%~dp0.."

rem ターゲットランタイム（既定: win-x64）
if "%~1"=="" (set "RID=win-x64") else (set "RID=%~1")

rem 最終的な出力先（Avalonia UI 版と分ける）
set "OUTDIR=%CD%\publish\mewui-%RID%-aot"

rem 発行の一時ステージング先
rem リポジトリ内へ直接発行すると、参照プロジェクト側のディレクトリにも publish が作られ、
rem さらに自己完結ランタイムのファイルが出力先へ混在する。
rem そのためリポジトリ外の一時領域へ発行し、必要なファイルのみを出力先へ配置する。
set "STAGE=%TEMP%\saveevidence-mew-aot-%RID%"

echo.
echo [INFO] Mode  : Native AOT (single exe)
echo [INFO] Target: %RID%
echo [INFO] Output: %OUTDIR%
echo.

if exist "%STAGE%" rd /s /q "%STAGE%"

dotnet publish "src\App.MewUI\src\App.MewUI.csproj" ^
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

rem 発行された exe 名を取得する（名前は csproj の AssemblyName が単一所有するため、ここでは決めない）
set "APP_NAME="
for %%f in ("%STAGE%\*.exe") do set "APP_NAME=%%~nxf"
if not defined APP_NAME (
  echo.
  echo [ERROR] Executable not found in: %STAGE%
  if exist "%STAGE%" rd /s /q "%STAGE%"
  popd
  exit /b 1
)

rem 出力先を作り直し、exe と DLL のみを配置する（.pdb は配布対象外）
if exist "%OUTDIR%" rd /s /q "%OUTDIR%"
mkdir "%OUTDIR%"
copy /y "%STAGE%\*.exe" "%OUTDIR%\" >nul
if exist "%STAGE%\*.dll" copy /y "%STAGE%\*.dll" "%OUTDIR%\" >nul
if exist "%STAGE%\*.json" copy /y "%STAGE%\*.json" "%OUTDIR%\" >nul
rd /s /q "%STAGE%"

echo.
echo [OK] Built: %OUTDIR%\%APP_NAME%
echo [INFO] 実行には同じフォルダーのネイティブ DLL が必要です（フォルダー単位で配布してください）。
popd
endlocal

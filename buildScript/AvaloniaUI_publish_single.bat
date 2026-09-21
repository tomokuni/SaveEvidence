@echo off
setlocal

rem ============================================================
rem  SaveEvidence (Avalonia UI) ビルドスクリプト
rem  Release / 自己完結 / 単一 exe を生成する
rem
rem  ⚠ 現在の配布は Native AOT（AvaloniaUI_publish_aot.bat）です。
rem    本スクリプトは AOT が使えない環境向けの代替です（exe が約 229 MB になります）。
rem
rem  方式: PublishSingleFile + SelfContained（csproj の Release 設定と同じ）
rem  Avalonia は SkiaSharp のネイティブ DLL に依存するが、単一 exe では
rem  自己展開（IncludeNativeLibrariesForSelfExtract）で同梱される。
rem  出力に exe 以外（ネイティブ DLL 等）が残る場合に備え、フォルダー単位で配布する。
rem ============================================================

rem スクリプトの場所（buildScript）からリポジトリルートへ移動する
pushd "%~dp0.."

rem ターゲットランタイム（既定: win-x64）
if "%~1"=="" (set "RID=win-x64") else (set "RID=%~1")

set "APP_NAME=SaveEvidence.exe"

rem 最終的な出力先
set "OUTDIR=%CD%\publish\avaloniaui-%RID%-single"

rem 発行の一時ステージング先
rem リポジトリ内へ直接発行すると、参照プロジェクト側にも publish が作られる。
rem そのためリポジトリ外の一時領域へ発行し、必要なファイルのみを出力先へ配置する。
set "STAGE=%TEMP%\saveevidence-single-avalonia-%RID%"

echo.
echo [INFO] Mode  : Single file (self-contained)
echo [INFO] Target: %RID%
echo [INFO] Output: %OUTDIR%
echo.

if exist "%STAGE%" rd /s /q "%STAGE%"

dotnet publish "src\app.AvaloniaUI\src\app.AvaloniaUI.csproj" ^
  -c Release ^
  -r "%RID%" ^
  -p:PublishSingleFile=true ^
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

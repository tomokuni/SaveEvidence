@echo off
setlocal

rem ============================================================
rem  SaveEvidence 配布物の共通ビルド手順（UI ごとの *_publish.bat から呼ばれる）
rem
rem  方式: 次を優先順に試し、最初に成功した方式で出力する。
rem    1. aot     PublishAot + PublishSingleFile（マネージドコードをネイティブへ）
rem    2. single  PublishSingleFile（自己完結ランタイムを 1 つの exe へ同梱）
rem    3. exe     通常の発行（exe + DLL をフォルダーで配布）
rem
rem  使い方:
rem    call publish_ui.bat <UI 名> <プロジェクトの相対パス> [RID]
rem    UI 名は .github/release-config.json の uis[].name と、出力フォルダーの接頭辞に使う
rem    （小文字表記。 例: mewui / avaloniaui / winforms / winui3）。
rem
rem  出力先は publish\<UI 名>-<RID>-<方式> とする。 方式を名前に含めるのは、配布物の姿
rem  （単一 exe か フォルダーか）を名前から読めるようにするためである。
rem  期待する出力先は .github/release-config.json の uis[].output が単一所有する
rem  （方式が変われば buildScript/verifyPublish.ps1 が「出力フォルダがありません」で気付く）。
rem
rem  方式ごとの可否は UI とフレームワークが決める（実測は各 *_publish.bat のコメントを参照）。
rem
rem  警告はエラーとして扱う（-warnaserror）。 これでトリミング・AOT の解析警告
rem  （IL2026 / IL2070 / IL3050 等）もビルドを失敗させる。 実測: 付けた瞬間に潜在不具合が
rem  出現した経緯がある（規則は .github/検証手順.md §18）。
rem
rem  配布対象外:
rem    - *.pdb       デバッグ記号
rem    - RestartAgent.exe  Windows App SDK が再起動 API 用に同封する補助 exe
rem      （本アプリは再起動 API を使わない。 除いても起動と終了が正常であることを実測済み。
rem        これを除かないと「配布物の exe は 1 個」という検査を満たせない）
rem  残りはすべて配置する。 拡張子で選別しない（AOT は exe + ネイティブ DLL、
rem  自己完結の WinUI 3 は exe + DLL + .pri + .winmd + .json を出し、どれか 1 つ欠けると壊れる。
rem  実測: WinUI 3 の配布物から .pri を落とすと、起動はしても終了時に 0x80008083
rem  （WinRT の stowed exception）で落ちた）。
rem ============================================================

if "%~1"=="" (
  echo [ERROR] UI 名が指定されていません。 使い方: call publish_ui.bat ^<UI 名^> ^<プロジェクト^> [RID]
  exit /b 1
)
if "%~2"=="" (
  echo [ERROR] プロジェクトが指定されていません。 使い方: call publish_ui.bat ^<UI 名^> ^<プロジェクト^> [RID]
  exit /b 1
)

set "UI=%~1"
set "PROJECT=%~2"
if "%~3"=="" (set "RID=win-x64") else (set "RID=%~3")

if not exist "%PROJECT%" (
  echo [ERROR] プロジェクトが見つかりません: %PROJECT%
  exit /b 1
)

rem 発行の一時ステージング先
rem リポジトリ内へ直接発行すると、参照プロジェクト側のディレクトリにも publish が作られる。
rem そのためリポジトリ外の一時領域へ発行し、必要なファイルのみを出力先へ配置する。
set "STAGE=%TEMP%\saveevidence-publish-%UI%-%RID%"
set "LOG=%TEMP%\saveevidence-publish-%UI%-%RID%.log"

echo.
echo [INFO] UI     : %UI%
echo [INFO] Target : %RID%
echo [INFO] Modes  : aot ^> single ^> exe（優先順に試行）
echo.

set "MODE="

call :TryPublish aot
if not errorlevel 1 set "MODE=aot"

if not defined MODE (
  call :TryPublish single
  if not errorlevel 1 set "MODE=single"
)

if not defined MODE (
  call :TryPublish exe
  if not errorlevel 1 set "MODE=exe"
)

if not defined MODE (
  echo.
  echo [ERROR] すべての方式で publish に失敗しました。 ログ: %LOG%
  if exist "%LOG%" type "%LOG%"
  if exist "%STAGE%" rd /s /q "%STAGE%"
  exit /b 1
)

set "OUTDIR=%CD%\publish\%UI%-%RID%-%MODE%"

rem 配置する exe を決める（名前は csproj の AssemblyName が単一所有するため、ここでは決めない）
set "APP_NAME="
set "FRAMEWORK_EXE=RestartAgent.exe"
for %%f in ("%STAGE%\*.exe") do if not defined APP_NAME if /i not "%%~nxf"=="%FRAMEWORK_EXE%" set "APP_NAME=%%~nxf"
if not defined APP_NAME (
  echo.
  echo [ERROR] 実行ファイルが見つかりません: %STAGE%
  if exist "%STAGE%" rd /s /q "%STAGE%"
  exit /b 1
)

if exist "%OUTDIR%" rd /s /q "%OUTDIR%"
mkdir "%OUTDIR%"
xcopy "%STAGE%\*" "%OUTDIR%\" /E /Q /Y >nul
del /q "%OUTDIR%\*.pdb" 2>nul
del /q "%OUTDIR%\%FRAMEWORK_EXE%" 2>nul
rd /s /q "%STAGE%"

echo [INFO] Mode   : %MODE%
echo [OK] Built: %OUTDIR%\%APP_NAME%
exit /b 0

rem ============================================================
rem  発行を 1 方式で試す。 成功なら errorlevel 0、失敗なら 1 を返す。
rem ============================================================
:TryPublish
set "TRY_MODE=%~1"
if exist "%STAGE%" rd /s /q "%STAGE%"

set "ARGS="
if "%TRY_MODE%"=="aot" set "ARGS=-p:PublishAot=true -p:SelfContained=true -p:PublishSingleFile=true"
if "%TRY_MODE%"=="single" set "ARGS=-p:PublishSingleFile=true -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true"

dotnet publish "%PROJECT%" -c Release -r "%RID%" -warnaserror %ARGS% -o "%STAGE%" >"%LOG%" 2>&1
if errorlevel 1 exit /b 1
if not exist "%STAGE%\*.exe" exit /b 1
exit /b 0

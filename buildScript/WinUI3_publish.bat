@echo off
setlocal

rem ============================================================
rem  SaveEvidence (WinUI 3) ビルドスクリプト
rem
rem  方式は buildScript/publish_ui.bat が aot ^> single ^> exe の優先順に選びます。
rem
rem  実測（2026-10-16）: 方式 1（Single + AOT）は使えません。
rem    CsWinRT の AOT 解析（CsWinRT1028 / CsWinRT1030）が要求する
rem    「WinRT ABI を渡る型を partial にする」「AllowUnsafeBlocks を有効にする」を
rem    本アプリと WinUI 3 コントロールの各プロジェクトへ広げても解決しません。
rem    Windows App SDK 1.8 時点で WinUI 3（C#）の Native AOT は未サポートであり、
rem    XAML ランタイムがリフレクションに依存するため、解析を回避しても成立しません。
rem    そのため方式 2（Single）で発行します。
rem
rem  ★ 単一ファイル化には「WinUI 3 のライブラリを RID 付きで先にビルドする」ことが必要です。
rem    理由: 単一ファイル化は各 WinUI 3 プロジェクトの .pri（リソース インデックス）を
rem    PublishSingleFile の入力に要求しますが（PRI175 / PRI252）、
rem    RuntimeIdentifier は ProjectReference へ伝播しないため、アプリの publish が
rem    参照プロジェクトをビルドしても .pri は TFM のみのフォルダへ出て、
rem    アプリが探す <TFM>\<RID>\ には置かれません。 ライブラリを -r 付きで直接ビルドすると
rem    <TFM>\<RID>\ へ .pri が出るため、その 1 手順を先に実行します（ライブラリに
rem    EnableMsixTooling は不要です。 実測）。
rem
rem  構成:
rem    - アンパッケージ（WindowsPackageType=None）+ Windows App SDK 自己完結
rem      （WindowsAppSDKSelfContained）のため、Windows App SDK のランタイムを同封します。
rem    - 本体プロジェクトの EnableMsixTooling は publish のために必要です（付けないと publish が
rem      SaveEvidence.WinUI3.pri を発行せず、起動はしても終了時に 0xC000027B / 0x80008083
rem      （WinRT の stowed exception）で落ちます。 実測）。
rem
rem  設定 JSON（重要）:
rem    SettingsJsonContext（JSON ソース生成コンテキスト）を SettingsService へ注入しています。
rem    コンテキストは Settings を参照する側のアセンブリに置く必要があります
rem    （同一コンパイル内では MVVM のソース生成結果を JSON のソース生成が見られないため）。
rem    設定の往復は回帰テスト（src/App.WinUI3/tests/AppJsonContext.Tests.cs）が
rem    検証します（buildScript/verify.ps1 のテスト実行に含まれます）。
rem ============================================================

rem スクリプトの場所（buildScript）からリポジトリルートへ移動する
pushd "%~dp0.."

rem ターゲットランタイム（既定: win-x64）
if "%~1"=="" (set "RID=win-x64") else (set "RID=%~1")

rem  WinUI 3 ライブラリの .pri を生成します（RID: %RID%）
rem  単一ファイル化は各 WinUI 3 プロジェクトの .pri（リソース インデックス）を publish の入力に要求しますが、
rem  RuntimeIdentifier は ProjectReference へ伝播しないため、アプリの publish が参照をビルドしても .pri は
rem  TFM のみのフォルダへ出て、アプリが探す <TFM>\<RID>\ には置かれません。 ライブラリを -r 付きで
rem  直接ビルドすると <TFM>\<RID>\ へ .pri が出ます（実測 2026-10-16）。
rem  失敗しても続行する（.pri を要さない方式では成功するため、ここで止めない）。
echo [INFO] WinUI 3 ライブラリの .pri を生成します（RID: %RID%）
for %%p in (
  "..\EsUtil\src\EsUtil.Feature.Loupe.ControlWinUI3\src\WinUI3\EsUtil.Feature.Loupe.ControlWinUI3.csproj"
  "..\EsUtil\src\EsUtil.Feature.Marquee.ControlWinUI3\src\WinUI3\EsUtil.Feature.Marquee.ControlWinUI3.csproj"
  "src\Platform.WinUI3\src\Platform.WinUI3.csproj"
  "src\Parts.CapturePreview.ControlWinUI3\src\Parts.CapturePreview.ControlWinUI3.csproj"
) do dotnet build %%p -c Release -r %RID% -v q --nologo >nul 2>&1

rem 単一ファイル化は csproj の `SaveEvidencePublishSingleFile` で門を作る（src\App.WinUI3\src\App.WinUI3.csproj）。
rem `-p:PublishSingleFile=true` を直接渡すと参照プロジェクトへ伝播し、WinUI 3 のライブラリが
rem EnableMsixTooling と WindowsAppSDKSelfContained を要求して失敗する（実測）。
set "SE_SINGLE_ARGS=-p:SaveEvidencePublishSingleFile=true"

call "%~dp0publish_ui.bat" winui3 "src\App.WinUI3\src\App.WinUI3.csproj" %*
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%

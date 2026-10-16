@echo off
setlocal

rem ============================================================
rem  SaveEvidence (WinUI 3) ビルドスクリプト
rem
rem  方式は buildScript/publish_ui.bat が aot ^> single ^> exe の優先順に選びます。
rem
rem  実測（2026-10-16）: 方式 1・2 は使えません。
rem    方式 1: CsWinRT の AOT 解析（CsWinRT1028 / CsWinRT1030）が要求する
rem      「WinRT ABI を渡る型を partial にする」「AllowUnsafeBlocks を有効にする」を
rem      本アプリと WinUI 3 コントロールの各プロジェクトへ広げても解決しません。
rem      Windows App SDK 1.8 時点で WinUI 3（C#）の Native AOT は未サポートであり、
rem      XAML ランタイムがリフレクションに依存するため、解析を回避しても成立しません。
rem    方式 2: 単一ファイル化は各 WinUI 3 プロジェクトの .pri（リソース インデックス）を
rem      PublishSingleFile の入力に要求します（PRI175 / PRI252）。 ライブラリ プロジェクトは
rem      EnableMsixTooling を付けても .pri を発行しないため、参照側で解決できません。
rem    そのため方式 3（フォルダー配布）で発行します。
rem
rem  構成:
rem    - アンパッケージ（WindowsPackageType=None）+ Windows App SDK 自己完結
rem      （WindowsAppSDKSelfContained）のため、Windows App SDK のランタイムを同封します。
rem    - EnableMsixTooling は本体プロジェクトが持ちます（付けないと publish が
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
call "%~dp0publish_ui.bat" winui3 "src\App.WinUI3\src\App.WinUI3.csproj" %*
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%

@echo off
setlocal

rem ============================================================
rem  SaveEvidence (WinForms) ビルドスクリプト
rem
rem  方式は buildScript/publish_ui.bat が aot ^> single ^> exe の優先順に選びます。
rem
rem  実測（2026-10-16）: 方式 1（Single + AOT）は使えません。
rem    NETSDK1175「Windows フォームに関して、トリミングの有効化はサポートおよび推奨されていません」。
rem    Native AOT はトリミングを前提とするため、WinForms では成立しません（WindowsForms の
rem    データ バインディングとリソースがトリミングで壊れるため、SDK が明示的に拒否します）。
rem    そのため方式 2（Single）で発行します。 exe 1 個で配布でき、ネイティブ ライブラリは
rem    起動時に一時領域へ展開されます（IncludeNativeLibrariesForSelfExtract）。
rem
rem  前提:
rem    Windows フォーム（System.Drawing.Common）は Windows 専用です（Windows 以外では動作しません）。
rem
rem  設定 JSON（重要）:
rem    本アプリは JSON ソース生成コンテキスト（SettingsJsonContext）を SettingsService へ
rem    注入しています（トリミング耐性と起動時間のため）。 コンテキストは Settings を参照する側の
rem    アセンブリに置く必要があります（同一コンパイル内では MVVM のソース生成結果を
rem    JSON のソース生成が見られないため）。
rem    設定の往復は配布物の検査（buildScript/verifyPublish.ps1 の「設定の往復」。
rem    .github/検証手順.md §18）が実物の exe で確かめます（本プロジェクトに専用の単体テストは
rem    持ちません）。
rem ============================================================

rem スクリプトの場所（buildScript）からリポジトリルートへ移動する
pushd "%~dp0.."
call "%~dp0publish_ui.bat" winforms "src\App.WinForms\src\App.WinForms.csproj" %*
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%

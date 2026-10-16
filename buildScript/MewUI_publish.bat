@echo off
setlocal

rem ============================================================
rem  SaveEvidence (MewUI) ビルドスクリプト
rem
rem  方式は buildScript/publish_ui.bat が aot ^> single ^> exe の優先順に選びます。
rem  実測（2026-10-16）: 方式 1（Single + AOT）で発行されます。
rem
rem  前提:
rem    Native AOT のリンクには Visual Studio の C++ ビルドツール（link.exe）が必要です。
rem
rem  構成:
rem    - マネージドコードは全て exe（SaveEvidence.MewUI.exe）へコンパイルされます。
rem    - SkiaSharp / OpenCvSharp のネイティブライブラリは exe へ同梱できないため、
rem      同じフォルダーへ並べて配布します。 ファイル数とサイズはコードの変更で変わるため、
rem      ここには書きません（publish の出力で確認します）。
rem
rem  設定 JSON（重要）:
rem    Native AOT ではリフレクションベースの System.Text.Json が動作しません
rem    （Settings の公開プロパティがトリミングで削除され、設定が空 {} になる）。
rem    そのためアプリ本体は JSON ソース生成コンテキスト（SettingsJsonContext）を
rem    SettingsService へ注入しています。 コンテキストは Settings を参照する側の
rem    アセンブリに置く必要があります（同一コンパイル内では MVVM のソース生成結果を
rem    JSON のソース生成が見られないため）。
rem ============================================================

rem スクリプトの場所（buildScript）からリポジトリルートへ移動する
pushd "%~dp0.."
call "%~dp0publish_ui.bat" mewui "src\App.MewUI\src\App.MewUI.csproj" %*
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%

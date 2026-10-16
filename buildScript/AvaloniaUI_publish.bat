@echo off
setlocal

rem ============================================================
rem  SaveEvidence (Avalonia UI) ビルドスクリプト
rem
rem  方式は buildScript/publish_ui.bat が aot ^> single ^> exe の優先順に選びます。
rem
rem  実測（2026-10-16）: 方式 1（Single + AOT）は使えません。
rem    EsUtil.Feature.Loupe.ControlAvaloniaUI の LoupeSourceResolver が
rem    Expression.Compile と Type.GetMethod で表示アダプタの契約（GetEsBitmap）を解決しており、
rem    トリミング / AOT の解析が IL2070（GetMethod の引数の注釈不足）と
rem    IL3050（Expression.Compile は動的コードを要求する）で失敗します（-warnaserror のためエラー）。
rem    そのため方式 2（Single）で発行します。
rem    ※ 方式 1 へ戻すには EsUtil 側の設計変更（公開インターフェースによる契約、または
rem      アダプタの型をホストが解決してルーペへ直接渡す形）が必要です。
rem
rem  前提:
rem    Native AOT のリンクには Visual Studio の C++ ビルドツール（link.exe）が必要です。
rem
rem  構成（方式 1 のとき）:
rem    - マネージドコードは全て exe（SaveEvidence.AvaloniaUI.exe）へコンパイルされます。
rem    - Avalonia / SkiaSharp / OpenCvSharp のネイティブライブラリは exe へ同梱できないため、
rem      同じフォルダーへ並べて配布します。
rem
rem  AOT で動作しない実装（実測。 方式 1 へ戻すときの前提）:
rem    - 実行時の XAML 解決（AvaloniaXamlLoader.Load(Uri)）は
rem      「No precompiled XAML found」で失敗します。 XAML はコンパイル時に取り込む形
rem      （x:Class 付きの Styles）にしてください。
rem
rem  設定 JSON（重要）:
rem    Native AOT ではリフレクションベースの System.Text.Json が動作しません
rem    （Settings の公開プロパティがトリミングで削除され、終了時に設定が空 {} になり全損します）。
rem    そのためアプリ本体は JSON ソース生成コンテキスト（SettingsJsonContext）を
rem    SettingsService へ注入しています。 コンテキストは Settings を参照する側の
rem    アセンブリに置く必要があります（同一コンパイル内では MVVM のソース生成結果を
rem    JSON のソース生成が見られないため）。
rem    設定の往復は回帰テスト（src/App.AvaloniaUI/tests/SettingsJsonContext.Tests.cs）が
rem    検証します（buildScript/verify.ps1 のテスト実行に含まれます）。
rem ============================================================

rem スクリプトの場所（buildScript）からリポジトリルートへ移動する
pushd "%~dp0.."
call "%~dp0publish_ui.bat" avaloniaui "src\App.AvaloniaUI\src\App.AvaloniaUI.csproj" %*
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%

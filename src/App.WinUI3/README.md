# App.WinUI3

SaveEvidence の **WinUI 3 版ホストアプリ**です。 UI 非依存のコア（`App.Core` / `EsUtil.Platform.Core` / `Parts.LineDetection`）を
そのまま使い、UI 層だけを WinUI 3（`Microsoft.WindowsAppSDK`）で実装します。

> **現状**: キャプチャ / フォルダビュー / Undo 一覧 / 設定の 4 ページと、撮影（スクリーン / ウィンドウ / 範囲）・
> プレビューの切り出し・履歴の移動・設定の永続化・グローバル ホットキー・ログが動作します。
> Avalonia 版 `App.AvaloniaUI` と MewUI 版 `App.MewUI` と同じ `settings.json` とログ ファイルを共有します。

## 1. 概要

SaveEvidence の **WinUI 3 版アプリ**（実行ファイル `SaveEvidence.WinUI3.exe`）です。 他の UI 版と同じ `App.Core` の
モデル・ViewModel と撮影スタック（`Parts.CapturePreview.*`）を共有し、**画面の構築だけを WinUI 3** で実装します。

## 2. 前提

- .NET 10 / Windows 10（19041）以降
- `Microsoft.WindowsAppSDK` 1.x（アンパッケージ / 自己完結で動作する）
- WinUI 3 の `App.xaml` 以外に XAML ファイルを持たず、**画面は C# のマークアップで組む**
  （MewUI 版と同じ方針。 ページごとの XAML とコードビハインドの二重管理を避ける）

## 3. 実行

```powershell
dotnet run --project src/App.WinUI3/src/App.WinUI3.csproj
```

アンパッケージ（MSIX なし）で起動します。 Windows App SDK は自己完結で配置するため、
**Windows App Runtime のインストールは不要**です。

## 4. ビルドと検証

```powershell
dotnet build src/App.WinUI3/src/App.WinUI3.csproj -c Debug
dotnet test src/App.WinUI3/tests/App.WinUI3.Tests.csproj -c Debug
```

- Debug / Release の両構成で **警告 0 件・エラー 0 件**、テストは**全件合格**が合否基準です
  （基準は [`../../.github/検証手順.md`](../../.github/検証手順.md) が所有します）。
- ソリューション全体は `dotnet build SaveEvidence.slnx` で検証します。

## 5. 配布形態

| 項目 | 内容 |
| --- | --- |
| 出力 | `SaveEvidence.WinUI3.exe`（`<AssemblyName>` で製品名を明示する） |
| パッケージ | アンパッケージ（`<WindowsPackageType>None</WindowsPackageType>`） |
| Windows App SDK | 自己完結（`<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>`） |
| 実行時識別子 | `win-x64`（自己完結配置はアーキテクチャの指定を要求する） |
| DPI 認識 | `app.manifest` で PerMonitorV2 を宣言する |
| 発行方式 | EXE（フォルダー配布）。 Single AOT と Single は WinUI 3 では成立しない（理由は [`buildScript/WinUI3_publish.bat`](../../buildScript/WinUI3_publish.bat) のコメント） |
| 配布スクリプト | [`buildScript/WinUI3_publish.bat`](../../buildScript/WinUI3_publish.bat)（方式は `buildScript/publish_ui.bat` が優先順に選ぶ） |

`<EnableMsixTooling>true</EnableMsixTooling>` は **publish のためだけ**に必要です（付けないと publish が自分自身の `.pri` を発行せず、
配布物の起動はしても終了時に `0xC000027B` / `0x80008083`（WinRT の stowed exception）で落ちます）。

**配布物はフォルダー単位で配置してください**（exe のほかに Windows App SDK のランタイム DLL と `.pri` / `.winmd` / `.json` が必要です）。
期待する出力先と exe 名は [`.github/release-config.json`](../../.github/release-config.json) が単一所有します。

## 6. 機能一覧

| 機能 | 内容 |
| --- | --- |
| 撮影 | スクリーン選択 / ウィンドウ選択 / 範囲選択（撮影スタック `Parts.CapturePreview.ControlWinUI3` へ委譲） |
| プレビュー | ズーム・パン・Fit 表示、カーソル座標の表示、自動切り出しの検出矩形の表示 |
| 切り出し | プレビューのドラッグで切り出し（`Ctrl+R` / ツールバー / メニュー） |
| クリップボード | プレビューのコピーと、クリップボードからの貼り付け |
| 保存 | ファイル名テンプレートから生成した名前で保存（上書き確認付き） |
| 編集履歴 | 戻す / やり直し（撮影・切り出しの直前へ戻せる） |
| フォルダビュー | 保存先フォルダの画像のサムネイル一覧（表示モード・ソート順・既定アプリで開く） |
| Undo 一覧 | 編集履歴と現在の画像のサムネイル一覧（表示モード・既定アプリで開く） |
| 設定 | テーマ / 保存先 / 画像 / キャプチャ / 矩形選択範囲の制限 / ホットキー / 外観（即時保存） |
| グローバル ホットキー | 3 種の撮影を他のアプリが前面でも開始できる（`RegisterHotKey`） |
| ログ | 他の UI 版と同一のファイルへ出力（`%LOCALAPPDATA%\SaveEvidence\logs\`） |

## 7. 画面構成

```text
src/App.WinUI3/
├── README.md
├── docs/
│   ├── SPEC_ExtFunc.md      # 外部機能仕様書（利用者から見た機能）
│   └── SPEC_System.md       # システム仕様書（構造・依存・設計判断）
├── src/
│   ├── App.WinUI3.csproj    # WinExe / net10.0-windows10.0.22621.0 / アンパッケージ
│   ├── app.manifest         # DPI 認識（PerMonitorV2）の宣言
│   ├── App.xaml             # XAML アプリケーション定義（既定スタイルの読み込みのみ）
│   ├── App.xaml.cs          # メイン ウィンドウの生成と UI スレッド例外の記録
│   ├── Program.cs           # エントリ ポイント（合成 → XAML アプリケーションの開始）
│   ├── AppHost.cs           # 合成ルート（DI コンテナと共有オブジェクト）
│   ├── AppServices.cs       # ページへ渡すコア オブジェクトの束
│   ├── Icons/
│   │   └── IconLoader.cs    # SVG アイコンの読み込みとラスタライズ
│   ├── Logging/
│   │   └── AppLog.cs        # ログ定義（LoggerMessage ソース生成）と出力構成
│   ├── Services/
│   │   ├── AppJsonContext.cs          # 設定の JSON ソース生成コンテキスト
│   │   ├── SettingsColorMath.cs       # 設定の色文字列 ⇔ 色の変換
│   │   ├── CaptureFlow.cs             # 撮影の開始（多重起動の防止）
│   │   ├── CaptureClipboard.cs        # クリップボードとの画像の受け渡し
│   │   ├── EditHistory.cs             # 編集履歴（戻す / やり直し）の実行
│   │   ├── GlobalHotKeyRegistration.cs # グローバル ホットキーの登録と配信
│   │   ├── WindowPlacementStore.cs    # ウィンドウ配置の保存と復元
│   │   ├── ImageThumbnailLoader.cs    # 画像ファイルの縮小デコードとキャッシュ
│   │   ├── ThumbnailBackground.cs     # 背景生成の共通処理（代替画像・UI スレッド反映）
│   │   └── FolderFileListReader.cs    # 一覧に出す画像ファイルの列挙
│   └── Views/
│       ├── MainWindow.cs        # ナビゲーション ペインとページの切り替え（先読みの開始）
│       ├── PageContent.cs       # ページの内容と表示ライフサイクルの処理
│       ├── AppStyles.cs         # 共通の寸法とテーマの適用
│       ├── CapturePage.cs       # キャプチャ ページ
│       ├── FolderViewPage.cs    # フォルダビュー ページ
│       ├── UndoPage.cs          # Undo 一覧ページ
│       ├── SettingsPage.cs      # 設定ページ
│       ├── ConfirmDialog.cs     # 上書き確認ダイアログ
│       ├── ThumbnailView.cs     # 縦横比を保って収める画像表示
│       └── WrapLayoutMath.cs    # 一覧の折り返し配置の計算
└── tests/
    ├── App.WinUI3.Tests.csproj
    ├── AppLog.Tests.cs
    ├── AppJsonContext.Tests.cs
    ├── CaptureSettingsMapper.Tests.cs
    ├── EditHistory.Tests.cs
    ├── FolderFileListReader.Tests.cs
    ├── ImageThumbnailLoader.Tests.cs
    ├── SettingsColorMath.Tests.cs
    ├── WrapLayoutMath.Tests.cs
    └── TestHelper.cs
```

`App.Core` の内部型は `InternalsVisibleTo` で公開されているため、サムネイル生成のスケジューラ（`ThumbnailScheduler` / `ThumbnailPriority`）・容量制限付きキャッシュ・画像拡張子の判定・設定の写像・表示モードの文言は
**コアの単一所有を使います**（本プロジェクトに同じ役割の型を持ちません）。

## 8. 設定ファイル

保存先は他の UI 版と**同一**です（`%LOCALAPPDATA%\SaveEvidence\settings.json`）。
一方の UI で変更した内容は、もう一方の UI の次回起動時に反映されます。

## 9. ログ

`%LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence<日付>.log` へ出力します（日次ローテーション・7 日保持）。
接頭辞は `[App.WinUI3]` で、他の UI 版のログと同じファイルへ追記されます
（規則は [`../../.github/ログ規約.md`](../../.github/ログ規約.md) が所有します）。

## 10. 関連ドキュメント

- [`docs/SPEC_ExtFunc.md`](docs/SPEC_ExtFunc.md) — 利用者から見た機能の仕様
- [`docs/SPEC_System.md`](docs/SPEC_System.md) — 構造・依存・設計判断
- [`../../README.md`](../../README.md) — リポジトリ全体の説明
- [`../../src/README.md`](../../src/README.md) — 層の帰属（Core / Presenter / UI のどれに置くか）

## 11. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。

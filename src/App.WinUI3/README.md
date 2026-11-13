# App.WinUI3

![Release](https://github.com/tomokuni/SaveEvidence/actions/workflows/release.yml/badge.svg)
![Build](https://github.com/tomokuni/SaveEvidence/actions/workflows/build.yml/badge.svg?branch=main)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2B-0078D6)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![WinUI3](https://img.shields.io/badge/UI-WinUI%203-0078D4)
![License](https://img.shields.io/badge/License-MIT-3DA639)

SaveEvidence の **WinUI 3 版ホストアプリ**です。 UI 非依存のコア（`App.Core` / `EsUtil.Platform.Core` / `Parts.LineDetection`）を
そのまま使い、UI 層だけを WinUI 3（`Microsoft.WindowsAppSDK`）で実装します。

> **現状**: キャプチャ / フォルダビュー / Undo 一覧 / 設定の 4 ページと、撮影（スクリーン / ウィンドウ / 範囲）・
> プレビューの切り出し・履歴の移動・設定の永続化・グローバル ホットキー・ログが動作します。
> Avalonia 版 `App.AvaloniaUI` と MewUI 版 `App.MewUI` と同じ `settings.json` を共有します（ログは実行ファイルごとに分かれます）。 タスク バーでも 1 つのアプリとしてまとまります（4 UI 版が同じ `AppUserModelId` を設定します）。

## 1. 概要

SaveEvidence の **WinUI 3 版アプリ**（実行ファイル `SaveEvidence.WinUI3.exe`）です。 他の UI 版と同じ `App.Core` の
モデル・ViewModel と撮影スタック（`Parts.CapturePreview.*`）を共有し、**画面の構築だけを WinUI 3** で実装します。

## 2. 前提

- .NET 10 / Windows 10（19041）以降
- `Microsoft.WindowsAppSDK` 2.6（アンパッケージ / フレームワーク依存）と **Windows App Runtime 2.6（x64）**
  （PC に無ければ起動時に案内を出して導入します）
- WinUI 3 の `App.xaml` 以外に XAML ファイルを持たず、**画面は C# のマークアップで組む**
  （MewUI 版と同じ方針。 ページごとの XAML とコードビハインドの二重管理を避ける）

## 3. 実行

```powershell
dotnet run --project src/App.WinUI3/src/App.WinUI3.csproj
```

アンパッケージ（MSIX なし）で起動します。 **Windows App SDK は同梱しない**（フレームワーク依存）ため、
**Windows App Runtime 2.6（x64）** が要ります。 無い場合は起動時に案内（「自動インストールする /
ダウンロードページを表示する / インストールせずにアプリを終了する」）を出します（§5）。

**起動は実行ファイル（アプリ ホスト）から行います**。 アンパッケージの WinUI 3 アプリは、
マニフェストによるアクティベーションと `resources.pri` の解決をアプリ ホストの exe が担うため、
`dotnet <アプリ>.dll` / `dotnet exec <アプリ>.dll` では起動できません（`Microsoft.UI.Xaml.dll` の
stowed exception `0xC000027B` で即座に終了します。 実測 2026-10-17）。 ビルド成果物の exe を直接起動してください。

```powershell
src/App.WinUI3/src/bin/Debug/net10.0-windows10.0.22621.0/win-x64/SaveEvidence.WinUI3.exe
```

VS Code のデバッグ構成（[`.vscode/launch.json`](../../.vscode/launch.json) の `WinUI3: SaveEvidence`）は、
この制約に合わせて **exe を起動する構成**（`type: coreclr`）にしています（`type: dotnet` は .NET ホスト経由で起動するため使えません）。 4 つの UI 版とも `coreclr` で起動します（デバッガーの設定 `logging.moduleLoad` / `justMyCode` が coreclr でだけ確実に効くため。 実測 2026-10-25）。

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
| Windows App SDK | フレームワーク依存（`<WindowsAppSDKSelfContained>false</WindowsAppSDKSelfContained>`）。 **Windows App Runtime 2.6（x64）** が要り、無ければ起動時に導入する（下記） |
| 実行時識別子 | `win-x64`（publish の `-r` で渡す。 Native AOT はアーキテクチャの指定を要求する。 `.csproj` へは書かない） |
| DPI 認識 | `app.manifest` で PerMonitorV2 を宣言する |
| 発行方式 | Native AOT のフォルダー配布（単一ファイル化はできない。 理由は [`buildScript/publish_WinUI3.bat`](../../buildScript/publish_WinUI3.bat) のコメント） |
| 配布スクリプト | [`buildScript/publish_WinUI3.bat`](../../buildScript/publish_WinUI3.bat)（方式は `buildScript/publish_ui.bat` が優先順に選ぶ） |

`<EnableMsixTooling>true</EnableMsixTooling>` は **publish のためだけ**に必要です（付けないと publish が自分自身の `.pri` を発行せず、
配布物の起動はしても終了時に `0xC000027B` / `0x80008083`（WinRT の stowed exception）で落ちます）。

配布物は **Native AOT のフォルダー配布**です（exe + ネイティブ DLL + `.pri`）。 **Native AOT はネイティブ DLL を
exe へ内包できず**、Windows App SDK はフレームワーク依存のまま単一ファイル化を拒否するためです（実測）。
AOT は `SaveEvidencePublishAot`（`buildScript/publish_WinUI3.bat` が渡す）で有効にし、通常のビルドとテストには
影響しません（コマンドラインの `-p:PublishAot=true` は参照プロジェクトへ伝播するため使いません）。
期待する出力先と exe 名は [`.github/release-config.json`](../../.github/release-config.json) が単一所有します。

**Windows App SDK は同梱しません**（`WindowsAppSDKSelfContained=false`）。 コード ページを他のアプリと共有できるため、
**起動が速くメモリも少なく**、Windows App SDK の保守も効きます（配布物の構成とサイズは `.github/verify/verifyPublish.ps1` が実測して表示します）。 **ランタイムの確認と導入は起動時**に
`EsUtil.Ui.WinUI3.Interop` の `Bootstrapping.WindowsAppRuntimeBootstrap` が行います（無ければ案内を出し、選ばれた
とおりに導入してから初期化します）。 そのため SDK の自動初期化（モジュール読み込み時に走り、導入はしない）は
`WindowsAppSdkBootstrapInitialize=false` で止め、案内のタスク ダイアログのために `app.manifest` で
Common-Controls v6 を宣言しています。

**配布物からは、使わないコンポーネント（ML = ONNX Runtime・DirectML、Windows AI）のネイティブ実体を除きます**
（実装は `src/App.WinUI3.csproj` の `TrimWinUI3PublishNoise`、規則は
[`.github/ビルド設定規約.md`](../../.github/ビルド設定規約.md) §5）。 本アプリはこれらの API を呼びません（ソース参照 0 件）。

### 5.1 自己完結 Single 版（何も導入できない環境向け）

**Windows App Runtime を導入できない環境**（Windows Sandbox・毎回初期化される VM 等）向けに、
**Windows App Runtime と .NET を同梱した単一 exe** を配布します（`SaveEvidence.WinUI3.Single.v<版数>.zip`）。

```powershell
& .\buildScript\publish_WinUI3.bat   # AOT 版と Single 版の両方を作る
```

- Windows App Runtime と .NET を**同梱**します（`SaveEvidenceSelfContained=true` + `PublishSingleFile=true`。
  `App.WinUI3.csproj` の条件付きプロパティ）。 ランタイムが 1 つも入っていない環境でもそのまま起動します。
- サイズは大きくなります（実測 2026-11-10: **293.5 MB の単一 exe**。 AOT 版は約 79 MB）。
- 起動時に一時フォルダーへ展開するため、初回の起動は AOT 版より遅くなります。
- **`-p:WindowsAppSDKSelfContained=true` をコマンド ラインへ直接渡さないでください**（参照プロジェクトへ
  伝播し、`WindowsAppSDKSelfContained requires a supported Windows architecture` で失敗します。 実測 2026-11-09）。
  スクリプトがプロジェクト内のプロパティを切り替えます。

**Windows App Runtime の状態は起動時のログに残ります**（成否・必要版・導入前後の版）。

```text
11:11:14.417 [INF] [Program] [App.WinUI3] Windows App Runtime を用意しました (required=2.6.5, before=2.6.5.0, 2.6.4.0, after=2.6.5.0, 2.6.4.0, initialized=true)
```

Single 版では `before` / `after` が `(なし)` のままです（同梱したランタイムを使うため）。

## 6. 機能一覧

| 機能 | 内容 |
| --- | --- |
| 撮影 | スクリーン選択 / ウィンドウ選択 / 範囲選択（撮影スタック `Parts.CapturePreview.ControlWinUI3` へ委譲） |
| プレビュー | ズーム・パン・Fit 表示、カーソル座標の表示、自動切り出しの検出矩形の表示 |
| ショートカット | 保存 `Ctrl+S` / コピー `Ctrl+C` / 貼り付け `Ctrl+V` / 切り出し `Ctrl+R`（メニューを開かずに効く） |
| 切り出し | プレビューのドラッグで切り出し（`Ctrl+R` / ツールバー / メニュー） |
| クリップボード | プレビューのコピーと、クリップボードからの貼り付け |
| 保存 | ファイル名テンプレートから生成した名前で保存（上書き確認付き）。 画像が変わっていない間・初期画像・テンプレートが無効な間は無効 |
| 自動保存 | ツールバーの「自動保存」トグルが有効な間は、画像が変わるたびに自動で保存 |
| 編集履歴 | 戻す / やり直し（撮影・切り出しの直前へ戻せる） |
| フォルダビュー | 保存先フォルダの画像のサムネイル一覧（表示モード・ソート順・既定アプリで開く・Delete キーでごみ箱へ移動・保存先パスのクリックでエクスプローラー） |
| Undo 一覧 | 編集履歴と現在の画像のサムネイル一覧（表示モード・既定アプリで開く） |
| 設定 | テーマ / 保存先 / 画像 / キャプチャ / 矩形選択範囲の制限 / ホットキー / 外観（即時保存） |
| グローバル ホットキー | 3 種の撮影を他のアプリが前面でも開始できる（`RegisterHotKey`） |
| ログ | 実行ファイルごとのファイルへ出力（`%LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.WinUI3.<yyyyMMdd>.log`） |

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
│   │   ├── CaptureFlow.cs             # 撮影の開始（多重起動の防止）
│   │   ├── CaptureClipboard.cs        # クリップボードとの画像の受け渡し
│   │   ├── EditHistory.cs             # 編集履歴（戻す / やり直し）の実行
│   │   ├── GlobalHotKeyRegistration.cs # グローバル ホットキーの登録と配信
│   │   ├── WindowPlacementStore.cs    # ウィンドウ配置の保存と復元
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
└── tests/
    ├── App.WinUI3.Tests.csproj
    ├── AppLog.Tests.cs
    ├── AppJsonContext.Tests.cs
    ├── CaptureSettingsMapper.Tests.cs
    ├── EditHistory.Tests.cs
    ├── FolderFileListReader.Tests.cs
        └── TestHelper.cs
```

`App.Core` の内部型は `InternalsVisibleTo` で公開されているため、サムネイル生成のスケジューラ（`ThumbnailScheduler` / `ThumbnailPriority`）・容量制限付きキャッシュ・画像拡張子の判定・設定の写像・表示モードの文言は
**コアの単一所有を使います**（本プロジェクトに同じ役割の型を持ちません）。

## 8. 設定ファイル

保存先は他の UI 版と**同一**です（`%LOCALAPPDATA%\SaveEvidence\settings.json`）。
一方の UI で変更した内容は、もう一方の UI の次回起動時に反映されます。

## 9. ログ

`%LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.WinUI3.<yyyyMMdd>.log` へ出力します（日次ローテーション・7 日保持。 ログは UI 版ごとに分かれます）。
接頭辞は `[App.WinUI3]` です。 起動時に Windows App Runtime の状態（必要版・導入前後の版・初期化の有無）も
記録するため、環境ごとの差をログだけで判別できます
（規則は [`../../.github/ログ規約.md`](../../.github/ログ規約.md) が所有します）。

## 10. 関連ドキュメント

- [`docs/SPEC_ExtFunc.md`](docs/SPEC_ExtFunc.md) — 利用者から見た機能の仕様
- [`docs/SPEC_System.md`](docs/SPEC_System.md) — 構造・依存・設計判断
- [`../../README.md`](../../README.md) — リポジトリ全体の説明
- [`../../src/README.md`](../../src/README.md) — 層の帰属（Core / Presenter / UI のどれに置くか）

## 11. ライセンス

[MIT License](LICENSE)

# App.Core

SaveEvidence 本体（Avalonia UI 実装 `App.AvaloniaUI`）および将来の MewUI ホストが共有する、**UI フレームワーク非依存のモデル・ユーティリティ・ViewModel** を提供するプロジェクトです。

## 1. 目的

アプリ本体のうち、UI フレームワークの型（`Avalonia.Point` / `PixelRect` / `Bitmap` 等）を使わない部分を本プロジェクトへ切り出すことで、同じロジックを MewUI ホストからも再利用できます。

## 2. 収録する型

| 型 | 公開 | 責務 |
| --- | --- | --- |
| `Enums.AppPage` | `public` | ナビゲーションページの識別子 |
| `Enums.ThemeMode` | `public` | テーマモード（`Description` 属性で表示名を保持） |
| `Models.Settings` | `public` | アプリケーション設定（変更通知付き）。 ウィンドウ位置は `EsRectInt`、状態は `EsWindowState` で保持 |
| `Services.ISettingsService` / `Services.SettingsService` | `public` | 設定の読み書き（`%LOCALAPPDATA%\SaveEvidence\settings.json`、`Lock` による排他制御）。 JSON ソース生成コンテキストを注入するとトリミング / Native AOT でも動作する |
| `Services.IImageWriter` | `public` | 画像（`EsReadOnlyBitmap`）のファイル書き込みの抽象 |
| `Services.EsBitmapImageWriter` | `public` | `IImageWriter` の**唯一の本番実装**（Windows 専用）。 コアの `EsReadOnlyBitmap.Save` へ委譲する。 両 UI が同じ実装を使う（保存結果が UI で食い違わない） |
| `Services.EsRectIntJsonConverter` | `internal` | `EsRectInt` の JSON コンバーター（`X` / `Y` / `Width` / `Height` のみを読み書き） |
| `Models.FileNameTemplate` | `public` | ファイル名テンプレートの解析・生成・検証（`GeneratedRegex` による最適化） |
| `Models.UndoHistory` | `public` | 画像の Undo / Redo スタック（`EsReadOnlyBitmap` の複製（`Copy`）を保持。 最大 5 段） |
| `Models.HotKeyParser` | `public` | ホットキー文字列（"Ctrl + Shift + Q" 等）の解析 |
| `Models.ThumbnailScheduler` | `internal` | 背景の処理（サムネイルの生成）をページの表示状態に応じた負荷で実行するスケジューラ（非表示では 1 件ずつ、表示中は並列。 優先度順に実行する） |
| `Models.ThumbnailPriority` | `internal` | サムネイル生成の優先度（見えている項目を先に、その中では上方を先に。 可視判定は純粋関数） |
| `Icons.AppIcons` | `internal` | 本アプリのアイコンの読み込み元（`App.Core` に埋め込んだ SVG）。 解析はコアの `SvgIconSource` が担う |
| `ViewModels.ViewModeDisplay` | `internal` | 表示モード（特大 / 大 / 中）の表示文字列変換 |
| `ViewModels.FolderViewViewModel` | `public` | フォルダビューの表示モード・ソート状態 |
| `ViewModels.UndoViewModel` | `public` | Undo 一覧の表示モード状態 |
| `ViewModels.MainViewModel` | `public` | メイン画面の状態（プレビュー画像・ズーム表示・保存・自動切り出し）。 UI 依存はコールバック（クリップボード / 上書き確認）で注入する |

`internal` の型は `InternalsVisibleTo`（`SaveEvidence` / `SaveEvidence.MewUI` / `App.AvaloniaUI.Tests` / `App.Core.Tests` / `App.MewUI.Tests`）で UI 実装とテストからのみ参照できます。

## 3. 名前空間

本プロジェクトは UI 非依存（Avalonia に依存しません）です。 **名前空間は `EsUtil.SaveEvidence.App.Core`、アセンブリ名は `App.Core`** です（ホストの `App.AvaloniaUI` とは名前空間もアセンブリも別）。

## 4. 設定ファイルの互換性

`EsRectInt` は専用の JSON コンバーター（`EsRectIntJsonConverter`）が `X` / `Y` / `Width` / `Height` のみを読み書きし、`EsWindowState` は enum の名前で書き出すため、**既存の `settings.json` はそのまま読み込めます**。

### 4.1 トリミング / Native AOT 対応

反射ベースのシリアル化はトリミングで `Settings` の公開プロパティが削除され、設定が空（`{}`）になります。
`SettingsService` は **ソース生成コンテキストを必須**とし、反射経路を持ちません（`new SettingsService(SettingsJsonContext.Default)` のように注入します）。

- コンテキストは **`Settings` を参照する側のアセンブリ**に置く必要があります（同一コンパイル内では MVVM の
  ソース生成結果を JSON のソース生成が見られないため）。
- 書式（インデント・enum の名前保存・矩形の 4 項目）はソース生成の有無にかかわらず共通で、設定ファイルの
  互換性は保たれます（`SettingsServiceJsonTests` が両経路の一致を検証します）。
- 反射リゾルバーを使った等価性の検証（`SettingsServiceJsonTests`）は**テスト側**でリゾルバーを用意します（本体にテスト専用の経路を置かない。 [`.github/テスト規約.md`](../../.github/テスト規約.md) §5）。

## 5. 依存方向

```text
App.AvaloniaUI（Avalonia 実装） / App.MewUI
  └──> App.Core（本プロジェクト）
         ├──> EsUtil.Platform.Core（画像コンテナ・コア型）
         └──> LineDetection（窓枠検出。UI 非依存）
```

UI フレームワークへ依存しません。 外部依存は MVVM の `CommunityToolkit.Mvvm`（UI フレームワーク非依存）のみです。

## 6. UI 依存の注入（依存性逆転）

`MainViewModel` は UI 操作が必要な箇所をコールバックとして受け取り、UI 型を直接持たないようにします。

| コールバック | 型 | 注入元（Avalonia 実装） |
| --- | --- | --- |
| `CopyToClipboardAsync` | `Func<EsReadOnlyBitmap, Task<bool>>` | `MainWindow`（`EsReadOnlyBitmap` → `Bitmap` 変換 + `Clipboard.SetBitmapAsync`） |
| `PasteFromClipboardAsync` | `Func<Task<EsBitmap?>>` | `MainWindow`（`Clipboard.TryGetBitmapAsync` + `Bitmap` → `EsBitmap` 変換。 画像を**作る**操作であるため書き換え可能な型） |
| `ConfirmOverwriteAsync` | `Func<string, Task<bool>>` | `MainWindow`（`ConfirmDialog`） |
| `BeforeEditAction` | `Action` | `CapturePage`（Undo スタックへのプッシュ） |
| `StartSelectionMode` | `Action<CaptureType>` | `CapturePage`（キャプチャの開始） |

画像の書き込み（`IImageWriter`）はコンストラクターで注入します（本番は本プロジェクトの `EsBitmapImageWriter`。 保存の実体はコアの `EsReadOnlyBitmap.Save`）。

## 7. ビルド

```powershell
dotnet build src/App.Core/src/App.Core.csproj
```

## 8. テスト

`tests/` が本プロジェクトの単体テストを持ちます。 UI 初期化が不要なため、すべて `[Fact]` で並列実行されます。

```powershell
dotnet test src/App.Core/tests/App.Core.Tests.csproj
```

## 9. ドキュメント

- [docs/SPEC_API.md](docs/SPEC_API.md) — 公開 API のリファレンス
- [docs/SPEC_System.md](docs/SPEC_System.md) — アーキテクチャ・依存・設計判断

## 10. 使い方

アプリ（UI 実装）からは、**UI 依存の操作をコールバックで注入**して使います。

```csharp
// 1. 設定サービスを生成する（JSON ソース生成コンテキストを渡す。 トリミング / Native AOT では必須）
var settingsService = new SettingsService(SettingsJsonContext.Default);

// 2. ViewModel を生成する（画像の書き込みは本プロジェクトの実装を注入する）
var viewModel = new MainViewModel(settingsService, new EsBitmapImageWriter());

// 3. UI 依存の操作（クリップボード・上書き確認・キャプチャ開始）をコールバックで設定する
viewModel.CopyToClipboardCallback = async image => { /* UI 型へ変換してクリップボードへ書く */ return true; };
viewModel.PasteFromClipboardCallback = async () => { /* クリップボードから読んで EsBitmap を返す */ return null; };
viewModel.ConfirmOverwriteAsync = async path => { /* 上書き確認ダイアログを出す */ return true; };
viewModel.StartSelectionMode = captureType => { /* キャプチャを開始する */ };
```

（`SettingsJsonContext` はホスト側の実装です。 画像の書き込みは本プロジェクトの `EsBitmapImageWriter`（Windows 専用）を使います。）

詳細は [docs/SPEC_API.md](docs/SPEC_API.md) を参照してください。

## 11. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。

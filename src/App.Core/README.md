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
| `Services.IImageWriter` | `public` | 画像（`EsBitmap`）のファイル書き込みの抽象。 具象実装（Avalonia 版は `AvaloniaImageWriter`）が各 UI プロジェクト側でエンコードする |
| `Services.EsRectIntJsonConverter` | `internal` | `EsRectInt` の JSON コンバーター（`X` / `Y` / `Width` / `Height` のみを読み書き） |
| `Models.FileNameTemplate` | `public` | ファイル名テンプレートの解析・生成・検証（`GeneratedRegex` による最適化） |
| `Models.UndoHistory` | `public` | 画像の Undo / Redo スタック（`EsBitmap` のクローンを保持。 最大 5 段） |
| `Models.HotKeyParser` | `public` | ホットキー文字列（"Ctrl + Shift + Q" 等）の解析 |
| `Models.CapacityLimitedCache` | `internal` | 容量制限付きの静的キャッシュ（超過分は破棄） |
| `Models.ImageFileExtensions` | `internal` | 画像ファイルの拡張子判定（フォルダビューの一覧抽出） |
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
| `CopyToClipboardAsync` | `Func<EsBitmap, Task<bool>>` | `MainWindow`（`EsBitmap` → `Bitmap` 変換 + `Clipboard.SetBitmapAsync`） |
| `PasteFromClipboardAsync` | `Func<Task<EsBitmap?>>` | `MainWindow`（`Clipboard.TryGetBitmapAsync` + `Bitmap` → `EsBitmap` 変換） |
| `ConfirmOverwriteAsync` | `Func<string, Task<bool>>` | `MainWindow`（`ConfirmDialog`） |
| `BeforeEditAction` | `Action` | `CapturePage`（Undo スタックへのプッシュ） |
| `StartSelectionMode` | `Action<CaptureType>` | `CapturePage`（キャプチャの開始） |

画像の書き込み（`IImageWriter`）はコンストラクターで注入します（UI 実装は `App.AvaloniaUI` 側の `AvaloniaImageWriter`）。

## 7. ビルド

```powershell
dotnet build src/App.Core/src/App.Core.csproj
```

## 8. テスト

`tests/` が本プロジェクトの単体テストを持ちます。 UI 初期化が不要なため、すべて `[Fact]` で並列実行されます。

```powershell
dotnet test src/App.Core/tests/App.Core.Tests.csproj
```

# SaveEvidence (app)

> プロジェクト名: `app.AvaloniaUI`（Avalonia UI 版）。MewUI 版は `app.MewUI`、UI 非依存のコアは `app.Core` にあります。

## 目次

- [SaveEvidence (app)](#saveevidence-app)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 機能一覧](#2-機能一覧)
  - [3. システム要件](#3-システム要件)
  - [4. ビルドと実行](#4-ビルドと実行)
  - [5. クイックスタート](#5-クイックスタート)
  - [6. 画面構成](#6-画面構成)
  - [7. キャプチャ](#7-キャプチャ)
  - [8. 画像の保存](#8-画像の保存)
    - [8.1 ファイル名テンプレート](#81-ファイル名テンプレート)
    - [8.2 保存先フォルダ](#82-保存先フォルダ)
  - [9. キーボードショートカット](#9-キーボードショートカット)
  - [10. フォルダビュー](#10-フォルダビュー)
  - [11. Undo 一覧](#11-undo-一覧)
  - [12. 設定画面](#12-設定画面)
  - [13. ログ](#13-ログ)
  - [14. 依存プロジェクト](#14-依存プロジェクト)
  - [15. ドキュメント](#15-ドキュメント)
  - [16. プロジェクト構成](#16-プロジェクト構成)
  - [17. ライセンス](#17-ライセンス)

---

## 1. 概要

Windows デスクトップ上の画面をキャプチャし、プレビュー・編集・保存するための Avalonia UI アプリケーションです（.NET 10.0 / C# 14 / MVVM）。

キャプチャ（全画面 / ウィンドウ選択 / 範囲選択）、切出し、ズーム・パン表示、ファイル名テンプレートによる保存、フォルダビュー、Undo / Redo 履歴、テーマ切替を提供します。

## 2. 機能一覧

- **キャプチャ**: 全画面（`ScreenSelect`）/ ウィンドウ選択（`WindowSelect`）/ 範囲選択（`AreaSelect`）の 3 モード
- **切出し（Crop）**: プレビュー画像上の選択矩形で元画像から切り出し
- **自動切り出し（LineDetection）**: ウィンドウ矩形を検出して自動切り出し（検出結果を画像上に水色の実線枠で表示・ホバー中は「赤枠 + 半透明の赤塗り」・矩形内のダブルクリックで確定・矩形外のダブルクリックまたは Escape でキャンセル。ホバー中もホイール・ドラッグでズーム・パンできる）
- **ズーム・パン**: Fit 表示・固定倍率（10〜500%）・パン・マウスホイールズーム（`CropViewControl` が内包する `ZoomViewControl` に委譲）
- **保存**: ファイル名テンプレート（日時プレースホルダ・オートインクリメント）・上書き確認
- **クリップボード連携**: 画像のコピー / 貼り付け
- **フォルダビュー**: 保存先フォルダの画像一覧（表示モード 特大/大/中・ソート・サムネイル）
- **Undo / Redo 履歴**: 編集履歴の一覧表示・復元
- **テーマ**: ライト / ダーク / システム（OS 追従）
- **グローバルホットキー**: 他アプリを操作中でもキャプチャを開始（設定画面でカスタマイズ）

## 3. システム要件

- .NET 10 SDK
- Windows 10 / 11
- Avalonia 12.1

## 4. ビルドと実行

```bash
# ソリューション全体をビルド
dotnet build SaveEvidence.slnx

# アプリを実行
dotnet run --project src/app.AvaloniaUI/src/app.AvaloniaUI.csproj

# テストを実行
dotnet test src/app.AvaloniaUI/tests/app.AvaloniaUI.Tests.csproj
```

### 配布ビルド（Native AOT）

```bat
buildScript\AvaloniaUI_publish_aot.bat
```

- 出力: `publish/avaloniaui-win-x64-aot`（`SaveEvidence.exe` + ネイティブ DLL）

| 方式 | 構成 | サイズ（実測） |
| --- | --- | --- |
| Native AOT | exe 1 個（27.7 MB）+ ネイティブ DLL 4 個 | 107 MB / 5 ファイル |

- ネイティブ DLL は `libSkiaSharp.dll` / `av_libglesv2.dll` / `libHarfBuzzSharp.dll` / `OpenCvSharpExtern.dll` です。
- OpenCV のネイティブは縮小構成（`OpenCvSharp5.runtime.win.slim`）のため、動画入出力（`videoio`）と FFmpeg（`opencv_videoio_ffmpeg500_64.dll` 約 31 MB）を同梱しません（本アプリは動画を扱わない）。
- **設定の JSON はソース生成コンテキスト（`SettingsJsonContext`）を `SettingsService` へ注入しています。**
  反射ベースのシリアル化は AOT / トリミングで公開プロパティが削除され、終了時に**設定が空（`{}`）になって全損します**（実測）。
  設定項目を追加するときは `Settings` を変更すれば自動で追従しますが、`SettingsJsonContext` を消したり別経路で `SettingsService` を生成しないでください。

## 5. クイックスタート

1. アプリを起動するとキャプチャページが表示されます。
2. ツールバーのキャプチャボタン（全画面 / ウィンドウ選択 / 範囲選択）またはグローバルホットキーでキャプチャを開始します。
3. キャプチャ結果がプレビューに表示されます。必要に応じて切出し・ズームを行います。
4. 「保存」ボタンまたは `Ctrl+S` で保存します（ファイル名テンプレートに従って自動命名）。

## 6. 画面構成

ナビゲーションパネル（左側）から 4 つのページを切り替えます。

| ページ | 内容 |
| --- | --- |
| キャプチャ | 画像プレビュー・切出し・ズーム・ルーペ |
| フォルダビュー | 保存先フォルダの画像一覧 |
| Undo 一覧 | Undo / Redo 履歴のサムネイル一覧 |
| 設定 | 外観 / キャプチャ / 保存 / 表示 / ホットキー / ラベル設定 |

## 7. キャプチャ

キャプチャは次のいずれかの方法で開始できます。

- ツールバーのキャプチャボタン
- キャプチャメニュー（`Capture`）
- グローバルホットキー（他アプリを操作中でも動作。設定画面でカスタマイズ可能）

キャプチャ中はアプリ本体が自動的に非表示になり、選択した領域（またはウィンドウ・全画面）がキャプチャされます。完了するとプレビューが更新されます。

## 8. 画像の保存

### 8.1 ファイル名テンプレート

保存ファイル名はテンプレート文字列から自動生成されます。`{ }` で囲まれたトークンを日時値へ置換します。

| トークン | 置換後 | 例 |
| --- | --- | --- |
| `{date}` | 日付（yyyyMMdd） | `20260819` |
| `{time}` | 時刻（HHmmss） | `235200` |
| `{yyyy}` | 年 | `2026` |
| `{MM}` | 月 | `08` |
| `{dd}` | 日 | `19` |
| `{HH}` | 時（24 時間） | `23` |
| `{mm}` | 分 | `52` |
| `{ss}` | 秒 | `00` |

- 複合トークン（例: `{yyyyMMdd}`）も使用できます。
- 中括弧自体は `{{` / `}}` でエスケープします。
- **オートインクリメント**: テンプレート内の一番右の数値連続が保存ごとに +1 されます（例: `screenshot_01.png` → `screenshot_02.png`。`99` → `100`、`099` → `100`）。

### 8.2 保存先フォルダ

保存先フォルダにはプレースホルダ `{MyPictures}`（大文字小文字を区別しない）を使用できます。

- 例: `{MyPictures}\SaveEvidence` → `C:\Users\<ユーザー名>\Pictures\SaveEvidence`
- フォルダが存在しない場合は保存時に自動的に作成されます。
- 存在しないパスは設定画面のテキストボックス枠が赤くなります。

## 9. キーボードショートカット

キャプチャページで有効な Ctrl ショートカットです。

| ショートカット | 機能 |
| --- | --- |
| `Ctrl+S` | 画像を保存 |
| `Ctrl+C` | 画像をクリップボードにコピー |
| `Ctrl+V` | クリップボードから貼り付け |
| `Ctrl+Z` | 元に戻す |
| `Ctrl+Y` | 元に戻すを取り消す（やり直し） |
| `Ctrl+R` | 切り出し |
| `Ctrl+=` / `Ctrl+Add` | ズームイン |
| `Ctrl+-` / `Ctrl+Subtract` | ズームアウト |

自動切り出し中は `Escape` でキャンセルできます（切り出しモードと同じ操作）。

## 10. フォルダビュー

保存先フォルダの画像ファイル（PNG / JPG / BMP / GIF / TIFF / WEBP）をサムネイル一覧表示します。

- **表示モード**: 特大 / 大 / 中（選択状態は自動保存され、次回起動時に復元）
- **ソート**: 名前の昇順 / 降順
- **ダブルクリック**: OS の既定アプリでファイルを開く
- **更新**: ツールバーの更新ボタンで再読込

## 11. Undo 一覧

キャプチャページの Undo / Redo スタックと現在のプレビュー画像を時系列順（Redo → 現在 → Undo）にサムネイル表示します。

- 現在の画像は青枠で強調表示されます。
- サムネイルのダブルクリックで一時ファイル（PNG）に保存し、OS の既定アプリで開きます。
- 表示モード（特大 / 大 / 中）は自動保存・復元されます。

## 12. 設定画面

設定画面では以下のカテゴリを編集できます。変更は即座に自動保存されます。

| カテゴリ | 内容 |
| --- | --- |
| 外観設定 | テーマ（ライト / ダーク / システム）、選択枠・暗転・ハンドル・ルーペ等の色・サイズ、「デフォルト値に戻す」 |
| キャプチャ設定 | メインウィンドウを隠す、選択矩形の最小サイズ 等 |
| 保存設定 | ファイル名テンプレート、保存先フォルダ |
| 表示設定 | フォルダビュー / Undo 一覧の表示モード、ナビパネルの開閉状態 等 |
| ホットキー設定 | 全画面 / ウィンドウ選択 / 範囲選択のグローバルホットキー（クリックしてキーを押すか、直接入力） |

## 13. ログ

ログは Serilog によりファイル出力されます。

- 場所: `%LOCALAPPDATA%\SaveEvidence\logs\SaveEvidence.log`
- 保持: 7 日で自動アーカイブ・削除（日次ローテーション）
- 設定ファイル: `%LOCALAPPDATA%\SaveEvidence\settings.json`（JSON・enum は文字列で保存）

## 14. 依存プロジェクト

| プロジェクト | 役割 |
| --- | --- |
| `app.Core` | UI 非依存のモデル・設定・ユーティリティ・表示用 ViewModel（設定モデル / 設定サービス / ファイル名テンプレート / Undo スタック / ホットキー解析 / 表示文字列） |
| `CaptureSessionControl.AvaloniaUI` | キャプチャ / 切出しセッション（Facade `CaptureSessionControl`）。`CropViewControl` / `ZoomViewControl` は本プロジェクト経由の推移的依存 |
| `ColorPickerControl` | 色選択コントロール（設定画面の色入力） |
| `Platform.AvaloniaUI` | プラットフォーム抽象（キャプチャ・要素検出・ウィンドウ操作・ホットキー） |
| `LineDetection` | ウィンドウ矩形検出（`LogicLineDetector`。自動切り出し用） |

## 15. ドキュメント

- `SPEC_ExtFunc.md`（外部機能仕様書）
- `SPEC_System.md`（システム仕様書）
- `SPEC_SettingTargetTable.md`（設定項目と反映先の対応表）

## 16. プロジェクト構成

UI 非依存のモデル・ユーティリティ・ViewModel は `app.Core` へ分離しています（マルチ UI 対応）。

```text
src/app.AvaloniaUI/
├── src/               # アプリ本体（Avalonia 実装）
│   ├── Views/         # ウィンドウ・ページ（MainWindow / CapturePage / FolderViewPage / UndoPage / SettingsPage）
│   ├── Services/      # AvaloniaImageWriter / CaptureSettingsMapper 等（Avalonia 依存の実装）
│   ├── Controls/      # BottomAlignWrapPanel
│   ├── Converters/    # EnumToDisplayNameConverter 等
│   ├── Icons/         # SVG アイコンの読み込み（IconLoader）
│   └── Logging/       # ログ定義（AppLog）
└── tests/             # ヘッドレステスト（app.AvaloniaUI.Tests）

src/app.Core/
├── src/
│   ├── Enum/          # AppPage / ThemeMode
│   ├── Models/        # Settings / FileNameTemplate / UndoStack / HotKeyParser / CapacityLimitedCache
│   ├── Services/      # ISettingsService / SettingsService / IImageWriter / EsPixelRectJsonConverter
│   └── ViewModels/    # MainViewModel / FolderViewViewModel / UndoViewModel / ViewModeDisplay
└── tests/             # UI 初期化不要の単体テスト（app.Core.Tests・並列実行）
```

`app.Core` の型を AXAML から参照する場合は、名前空間が `app.AvaloniaUI` と同一のため
`xmlns:vm="clr-namespace:Estable.SaveEvidence.ViewModels;assembly=app.Core"` のように **`assembly=` の指定が必要**です。

## 17. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。

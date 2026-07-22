# プログラム詳細仕様書 SpecProgram.md

**SaveEvidence** — 画面キャプチャユーティリティ

---

## 目次

- [1. 名前空間一覧](#1-名前空間一覧)
- [2. Program.cs（エントリポイント）](#2-programcsエントリポイント)
- [3. Enum（列挙型）](#3-enum列挙型)
- [4. Models（モデル）](#4-modelsモデル)
- [5. ViewModels](#5-viewmodels)
- [6. Views（フォーム）](#6-viewsフォーム)
- [7. Services](#7-services)
- [8. 非公開 API（internal / private）](#8-非公開-apiinternal--private)
- [9. 定数一覧](#9-定数一覧)
- [10. データ変換仕様](#10-データ変換仕様)

---

## 1. 名前空間一覧

| 名前空間 | 説明 |
| --- | --- |
| `app` | エントリポイント（Program.cs） |
| `app.Enum` | 列挙型定義 |
| `app.Models` | データモデル・ビジネスロジック |
| `app.ViewModels` | ViewModel |
| `app.Views` | フォーム・UI |
| `app.Services` | サービス（設定管理） |

---

## 2. Program.cs（エントリポイント）

### 2.1 クラス: `Program`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `internal static class` |
| **責務** | アプリケーションのエントリポイント |

### 2.2 メソッド: `Main`

| 項目 | 内容 |
| --- | --- |
| **シグネチャ** | `[STAThread] static void Main()` |
| **概要** | アプリケーションのメインエントリポイント |
| **処理フロー** | 1. グローバル例外ハンドラの設定（`SetUnhandledExceptionMode`）\<br /\>2. HighDPI モード（`PerMonitorV2`）の設定\<br /\>3. システムカラーモードの設定\<br /\>4. `SettingsService` の生成\<br /\>5. `MainForm` の生成とメインループ開始 |

---

## 3. Enum（列挙型）

### 3.1 `CaptureType.cs`

| 分類 | 内容 |
| --- | --- |
| **名前空間** | `app.Enum` |
| **定義** | `public enum CaptureType` |
| **概要** | 画面キャプチャの取得方法を指定する |

| メンバー | 値 | 説明 |
| --- | --- | --- |
| `SelectScreen` | 0 | スクリーン選択 |
| `WindowSelect` | 1 | ウィンドウ選択 |
| `AreaSelect` | 2 | 範囲選択 |

### 3.2 `HotKeyModifiers.cs`

| 分類 | 内容 |
| --- | --- |
| **名前空間** | `app.Enum` |
| **定義** | `[Flags] public enum HotKeyModifiers` |
| **概要** | ホットキーの修飾キーを指定するフラグ列挙型 |

| メンバー | 値 | 説明 |
| --- | --- | --- |
| `None` | 0 | 修飾キーなし |
| `Alt` | 0x0001 | Alt キー |
| `Control` | 0x0002 | Ctrl キー |
| `Shift` | 0x0004 | Shift キー |
| `Windows` | 0x0008 | Windows キー |

### 3.3 `LoupeMode.cs`

| 分類 | 内容 |
| --- | --- |
| **名前空間** | `app.Enum` |
| **定義** | `public enum LoupeMode` |
| **概要** | ルーペ（拡大鏡）の表示モード |

| メンバー | 値 | 説明 |
| --- | --- | --- |
| `Show` | 0 | 常に表示 |
| `Hide` | 1 | 常に非表示 |
| `Auto` | 2 | 範囲選択中のみ自動表示 |

### 3.4 `WindowCaptureMode.cs`

| 分類 | 内容 |
| --- | --- |
| **名前空間** | `app.Enum` |
| **定義** | `public enum WindowCaptureMode` |
| **概要** | ウィンドウキャプチャ時の画像取得方式 |

| メンバー | 値 | 説明 |
| --- | --- | --- |
| `PrintWindow` | 0 | PrintWindow API を使用（DWM から直接取得） |
| `CopyFromScreen` | 1 | CopyFromScreen を使用（従来方式） |

---

## 4. Models（モデル）

### 4.1 `CaptureManager.cs`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `public static partial class` |
| **名前空間** | `app.Models` |
| **責務** | Win32 API を使用した画面キャプチャ機能 |
| **特徴** | 全メソッドが静的、ソースジェネレーター対応のため `partial` で宣言 |

#### 4.1.1 静的メソッド

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `GetAllScreenBounds` | `static (int Index, Rectangle Bounds, string DeviceName)[]` | 全スクリーンの境界情報を取得 |
| `CaptureArea` | `static Bitmap(Rectangle bounds)` | 指定された画面領域をキャプチャ |
| `CaptureWindow` | `static Bitmap(IntPtr hWnd, WindowCaptureMode mode = PrintWindow)` | 指定ウィンドウをキャプチャ。PrintWindow 失敗時は CopyFromScreen に自動フォールバック |
| `GetWindowUnderCursor` | `static IntPtr()` | マウスカーソル下のトップレベルウィンドウハンドルを取得 |
| `GetWindowVisibleRect` | `static Rectangle(IntPtr hWnd)` | 半透明影を除外した可視矩形領域を取得 |
| `GetWindowRect` | `static Rectangle(IntPtr hWnd)` | ウィンドウの矩形領域を取得 |

#### 4.1.2 内部メソッド（private）

| メソッド | 説明 |
| --- | --- |
| `TryPrintWindow` | PrintWindow を試行し、有効なビットマップが取得できた場合のみ返す |
| `IsBitmapValid` | PrintWindow で生成されたビットマップが有効な内容を含むか 5 箇所サンプリングで判定 |

#### 4.1.3 Win32 P/Invoke

| メソッド | DLL | エントリポイント |
| --- | --- | --- |
| `GetWindowRectNative` | user32.dll | `GetWindowRect` |
| `WindowFromPoint` | user32.dll | `WindowFromPoint` |
| `GetCursorPosNative` | user32.dll | `GetCursorPos` |
| `GetAncestor` | user32.dll | `GetAncestor` |
| `PrintWindow` | user32.dll | `PrintWindow` |
| `DwmGetWindowAttribute` | dwmapi.dll | `DwmGetWindowAttribute` |

#### 4.1.4 内部構造体

| 構造体 | フィールド |
| --- | --- |
| `RECT` | `int Left, Top, Right, Bottom` |
| `POINT` | `int X, Y` |

#### 4.1.5 定数

| 定数 | 値 | 説明 |
| --- | --- | --- |
| `GA_ROOT` | 2 | ルートトップレベルウィンドウ |
| `PW_CLIENTONLY` | 0x00000001 | クライアント領域のみ描画 |
| `PW_RENDERFULLCONTENT` | 0x00000002 | DWM に完全描画を要求 |
| `DWMWA_EXTENDED_FRAME_BOUNDS` | 9 | 半透明影を除外した可視矩形 |

---

### 4.2 `HotKeyManager.cs`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `public sealed partial class : IDisposable` |
| **名前空間** | `app.Models` |
| **責務** | グローバルホットキーの登録・解除・管理 |
| **コンストラクタ** | `HotKeyManager(IntPtr hWnd)` — ホットキー通知を受け取るウィンドウハンドルを指定 |

#### 4.2.1 パブリックメソッド

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `Register` | `void(HotKeySetting setting, CaptureType captureType)` | ホットキーを登録。既存の同一 CaptureType は解除後に登録 |
| `Unregister` | `void(CaptureType captureType)` | 特定のキャプチャタイプのホットキーを解除 |
| `UnregisterAll` | `void()` | 全ホットキーを解除 |
| `ProcessHotKeyMessage` | `CaptureType?(Message msg)` | WM_HOTKEY メッセージを処理し、対応する CaptureType を返す |
| `RegisterAll` | `void(Settings settings)` | 設定オブジェクトの内容に基づいて 3 種類のホットキーを全て再登録 |
| `Dispose` | `void()` | 全ホットキーの登録を解除 |

#### 4.2.2 内部フィールド

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `_hWnd` | `IntPtr` | ホットキー通知を受け取るウィンドウハンドル |
| `_currentId` | `int` | ホットキー ID のカウンター |
| `_registeredHotKeys` | `Dictionary<int, (HotKeySetting, CaptureType)>` | 登録済みホットキーの管理 |

#### 4.2.3 Win32 P/Invoke

| メソッド | DLL | エントリポイント |
| --- | --- | --- |
| `RegisterHotKey` | user32.dll | `RegisterHotKey` |
| `UnregisterHotKey` | user32.dll | `UnregisterHotKey` |

---

### 4.3 `HotKeySetting.cs`

| 分類 | 内容 |
| --- | --- |
| **定義** | `public sealed record HotKeySetting(HotKeyModifiers Modifiers, Keys Key)` |
| **名前空間** | `app.Models` |
| **責務** | ホットキーの組合せ（修飾キー＋キー）を表す値オブジェクト |

#### 4.3.1 メソッド

| メソッド | 説明 |
| --- | --- |
| `ToString()` | "Ctrl + Shift + Q" のようなユーザー可読な文字列を返す |
| `KeyToSymbolString`（private static） | 特殊キー（D0-D9、Oemtilde 等）を記号文字列に変換 |

---

### 4.4 `Settings.cs`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `public sealed partial class : ObservableObject` |
| **名前空間** | `app.Models` |
| **責務** | アプリケーション設定のデータモデル（変更通知対応） |
| **特徴** | CommunityToolkit.Mvvm ソースジェネレーターにより `[ObservableProperty]` が自動コード生成 |

#### 4.4.1 ObservableProperty 一覧

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `SelectScreenHotKey` | `HotKeySetting` | Ctrl+Shift+Q | スクリーン選択キャプチャのホットキー |
| `WindowSelectHotKey` | `HotKeySetting` | Ctrl+Shift+W | ウィンドウ選択キャプチャのホットキー |
| `AreaSelectHotKey` | `HotKeySetting` | Ctrl+Shift+E | 範囲選択キャプチャのホットキー |
| `FileNameTemplate` | `string` | `screenshot_{date}_{time}.png` | ファイル名テンプレート |
| `SaveFolderPath` | `string` | 空文字列 | 保存先フォルダパス |
| `FolderViewModeIndex` | `int` | 1 | フォルダビューアー表示モード |
| `FolderSortAscending` | `bool` | true | ソート昇順 |
| `MainFormBounds` | `Rectangle?` | null | メインフォーム位置・サイズ |
| `MainFormWindowState` | `FormWindowState` | Normal | メインフォームウィンドウ状態 |
| `FolderViewFormBounds` | `Rectangle?` | null | フォルダビューアー位置・サイズ |
| `FolderExtraLargeIconSize` | `int` | 320 | 特大アイコンサイズ |
| `FolderLargeIconSize` | `int` | 240 | 大アイコンサイズ |
| `FolderMediumIconSize` | `int` | 160 | 中アイコンサイズ |
| `CenterAlign` | `bool` | true | 中央寄せ |
| `CaptureBorderColor` | `string` | `White` | キャプチャ選択枠色 |
| `CropBorderColor` | `string` | `White` | 切り出し選択枠色 |
| `LoupeCrossColor` | `string` | `Red` | ルーペ十字線色 |
| `LoupeFrameColor` | `string` | `White` | ルーペ外枠色 |
| `LoupeFrameWidth` | `int` | 2 | ルーペ外枠太さ |
| `LoupeZoomLevel` | `int` | 8 | ルーペ拡大率 |
| `LoupeSize` | `int` | 170 | ルーペサイズ |
| `LoupeModeValue` | `LoupeMode` | Hide | ルーペ表示モード |
| `CaptureMode` | `WindowCaptureMode` | PrintWindow | キャプチャ方式 |

---

### 4.5 `ImageProcessor.cs`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `public static class` |
| **名前空間** | `app.Models` |
| **責務** | 画像の切り出し処理と、背景色自動判別によるウィンドウ領域検出 |

#### 4.5.1 パブリックメソッド

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `Crop` | `static Bitmap(Image source, Rectangle bounds)` | 画像から指定矩形領域を切り出す |
| `DetectAndCropWindow` | `static Bitmap(Image image)` | 画像内のウィンドウ領域を自動判定して切り出す。検出失敗時は元画像のコピーを返す |

#### 4.5.2 内部メソッド（private static）

| メソッド | 説明 |
| --- | --- |
| `DetectWindowBounds` | 四隅の背景色からウィンドウ領域の矩形を自動検出 |
| `ColorToArgbKey` | 色を int 値に変換（GroupBy のキー生成用） |
| `ScanTopBg` | 上端から背景色が続く行数をスキャン |
| `ScanBottomBg` | 下端から背景色が続く行数をスキャン |
| `ScanLeftBg` | 左端から背景色が続く列をスキャン |
| `ScanRightBg` | 右端から背景色が続く列をスキャン |
| `IsRowColor` | 指定行の全サンプルが指定色に類似するか判定（20px 間隔） |
| `IsColColor` | 指定列の全サンプルが指定色に類似するか判定（20px 間隔） |
| `IsColorSimilar` | 2 色の RGB 差分合計が 30 未満か判定 |

---

### 4.6 `FileNameTemplate.cs`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `public sealed partial class`（static メソッドのみ） |
| **名前空間** | `app.Models` |
| **責務** | ファイル名テンプレートの解析・生成 |
| **特徴** | `[GeneratedRegex]` による正規表現のコンパイル時最適化 |

#### 4.6.1 パブリックメソッド

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `Generate` | `static string(string template)` | テンプレートから実際のファイル名を生成。`{date}` → `yyyyMMdd`、`{time}` → `HHmmss` |
| `IncrementRightmostNumber` | `static string(string text)` | テンプレート内の右端数値を +1 し桁数を維持 |
| `IsValidTemplate` | `static bool(string template, out string? errorMessage)` | ファイル名テンプレートとして使用可能か検証 |

#### 4.6.2 内部メソッド（private）

| メソッド | 説明 |
| --- | --- |
| `NumberPattern`（partial） | 数値の塊を検出する `[GeneratedRegex("\\d+")]` |

#### 4.6.3 数値インクリメントのアルゴリズム

1. 正規表現で全ての数値の塊を検出
2. 最後（右端）の数値の塊を選択
3. `decimal.TryParse` でパースを試行 → 成功時は `decimal++`
4. decimal で扱えない場合は `BigInteger.TryParse` でパース → `BigInteger++`
5. 元の桁数で `PadLeft` によるゼロ埋め

---

### 4.7 `CropSelection.cs`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `public sealed class` |
| **名前空間** | `app.Models` |
| **責務** | プレビュー画像上の手動切り出し選択状態を管理 |

#### 4.7.1 内部列挙型

| 列挙型 | メンバー |
| --- | --- |
| `HandleType`（private） | `None, TopLeft, TopCenter, TopRight, MiddleLeft, MiddleRight, BottomLeft, BottomCenter, BottomRight` |

#### 4.7.2 パブリックプロパティ

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `SelectionRect` | `Rectangle?`（get） | 現在の選択領域（画像座標）。未選択は null |
| `IsHandleActive` | `bool`（get） | ハンドルがアクティブかどうか |
| `IsDragging` | `bool`（get） | ドラッグ中かどうか |

#### 4.7.3 パブリックメソッド

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `Reset` | `void()` | 選択状態を全てリセット |
| `SetSelection` | `void(Rectangle rect)` | 外部から選択領域を設定 |
| `MouseDown` | `void(Point imgPoint, Size imageSize)` | マウスダウン処理（ハンドルヒット判定→リサイズ/移動/新規選択） |
| `MouseMove` | `void(Point imgPoint, Size imageSize)` | マウス移動処理（リサイズ/移動/新規選択の実行） |
| `MouseUp` | `void()` | マウスアップ処理（操作の確定） |
| `GetCursor` | `Cursor?(Point imgPoint)` | カーソル位置に対応するカーソル形状を返す（画像座標ベース） |
| `GetCursorClient` | `Cursor?(Point clientPt, Point[] handlePts, Rectangle clientSel)` | カーソル位置に対応するカーソル形状を返す（クライアント座標ベース） |
| `GetHandleClientPoints` | `Point[](Rectangle selRect, double zoom, int offsetX, int offsetY)` | 8 方向ハンドルのクライアント座標を取得 |
| `HasSelection` | `bool`（get） | 選択領域が存在するかどうか |

#### 4.7.4 内部定数

| 定数 | 値 | 説明 |
| --- | --- | --- |
| `HandleSize` | 24 | ハンドルのサイズ（ピクセル） |

---

## 5. ViewModels

### 5.1 `MainViewModel.cs`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `public sealed partial class : ObservableObject` |
| **名前空間** | `app.ViewModels` |
| **責務** | メイン画面の状態管理・コマンド処理 |

#### 5.1.1 依存関係

- `ISettingsService _settingsService` — 設定管理サービス

#### 5.1.2 ObservableProperty

| プロパティ | 型 | 説明 | 変更通知連動 |
| --- | --- | --- | --- |
| `PreviewImage` | `Image?` | プレビュー画像 | `HasPreviewImage`, `CanCopy`, `CanSave`, `StatusText` |
| `IsSaved` | `bool` | 画像が保存済みか | `StatusText` |
| `FileNameTemplateText` | `string` | ファイル名テンプレート | `CurrentFileNamePreview`, `StatusText`, `HasValidTemplate`, `ValidationMessage`, `CanSave` |
| `SaveFolderPath` | `string` | 保存先フォルダパス | `SaveFolderDisplayName` |
| `SaveFolderDisplayName` | `string` | 保存先フォルダ表示名 | |
| `ZoomPercent` | `int` | 現在のズーム率（View 側から設定） | |
| `HasSelection` | `bool` | 切り出し選択範囲の有無（View 側から設定） | |
| `CanZoomIn` | `bool` | ズームイン可能か（View 側から設定） | |
| `CanZoomOut` | `bool` | ズームアウト可能か（View 側から設定） | |

#### 5.1.3 読み取り専用プロパティ

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `HasPreviewImage` | `bool` | プレビュー画像の有無 |
| `CanCopy` | `bool` | クリップボードコピー可能か |
| `CanSave` | `bool` | 保存可能か（画像あり + 有効なテンプレート） |
| `CurrentFileNamePreview` | `string` | 現在のファイル名プレビュー |
| `StatusText` | `string` | 現在の状態テキスト |
| `HasValidTemplate` | `bool` | ファイル名テンプレートの有効性 |
| `ValidationMessage` | `string?` | テンプレート検証エラーメッセージ |

#### 5.1.4 パブリックメソッド

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `CaptureScreenArea` | `void(Rectangle bounds)` | 指定領域をキャプチャしてプレビューに設定 |
| `SetPreviewImage` | `void(Image image)` | 画像をプレビューとして設定 |
| `CopyToClipboard` | `void()` | 画像をクリップボードにコピー（RelayCommand） |
| `PasteFromClipboard` | `bool()` | クリップボードから画像を貼り付け |
| `CropPreview` | `void(Rectangle bounds)`（private RelayCommand） | 指定範囲でプレビュー画像を切り出し |
| `AutoCropWindow` | `void()`（private RelayCommand） | ウィンドウ領域を自動判定して切り出し |
| `SaveImage` | `bool()` | 現在のプレビュー画像を保存。保存後に数値インクリメント |
| `OpenSaveFolder` | `void()`（private RelayCommand） | 保存先フォルダをエクスプローラーで開く |
| `ReloadSettings` | `void()` | 設定を再読み込み |

#### 5.1.5 partial メソッド（ソースジェネレーター生成）

| メソッド | 説明 |
| --- | --- |
| `OnPreviewImageChanging` | 変更前に古い画像を Dispose |
| `OnFileNameTemplateTextChanged` | テンプレート変更時に設定を保存 |
| `OnSaveFolderPathChanged` | 保存先変更時に設定を保存し表示名を更新 |

#### 5.1.6 内部フィールド

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `_lastSavedFileName` | `string` | 最後に保存したファイル名 |

---

### 5.2 `FolderViewViewModel.cs`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `public sealed partial class : ObservableObject` |
| **名前空間** | `app.ViewModels` |
| **責務** | フォルダビューアーの状態管理 |

#### 5.2.1 ObservableProperty

| プロパティ | 型 | 説明 | 変更通知連動 |
| --- | --- | --- | --- |
| `ItemCount` | `int` | アイテム数 | `ItemCountText` |
| `SelectedViewModeIndex` | `int` | 表示モードインデックス（0～4） | `ViewModeText` |
| `IsSortAscending` | `bool` | ソート昇順 | `SortStatusText` |
| `FolderPathText` | `string` | フォルダパス | |

#### 5.2.2 読み取り専用プロパティ

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `ItemCountText` | `string` | "10 個のアイテム" |
| `ViewModeText` | `string` | "特大" / "大" / "中" / "一覧" / "詳細" |
| `SortStatusText` | `string` | "名前の昇順(▲)" / "名前の降順(▼)" |

#### 5.2.3 内部定数

| フィールド | 値 |
| --- | --- |
| `s_viewModeNames` | `["特大", "大", "中", "一覧", "詳細"]` |

---

## 6. Views（フォーム）

### 6.1 `MainForm.cs`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `public sealed partial class : Form` |
| **名前空間** | `app.Views` |
| **責務** | メインウィンドウ。キャプチャ・プレビュー・編集・保存の全機能を統合 |

#### 6.1.1 依存関係

- `MainViewModel _viewModel` — ViewModel
- `HotKeyManager? _hotKeyManager` — ホットキー管理
- `SettingsService _settingsService` — 設定サービス

#### 6.1.2 内部フィールド

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `_isExecutingCapture` | `bool` | キャプチャ実行中フラグ（二重起動防止） |
| `_isCropMode` | `bool` | 切出しモードフラグ |
| `_cropSelection` | `CropSelection` | 切出し選択管理 |
| `_zoomPercent` | `int` | 現在のズーム率（0=自動） |
| `_zoomIndex` | `int` | 現在のズーム段階インデックス |
| `_scrollX, _scrollY` | `int` | スクロール位置 |
| `_undoStack` | `List<Image>` | アンドゥスタック（最大5） |

#### 6.1.3 主要イベントハンドラ

| ハンドラ | 説明 |
| --- | --- |
| `BtnSelectScreen_Click` | スクリーン選択キャプチャ開始 |
| `BtnWindowSelect_Click` | ウィンドウ選択キャプチャ開始 |
| `BtnAreaSelect_Click` | 範囲選択キャプチャ開始 |
| `PerformAutoCrop` | 自動ウィンドウ検出切り出し |
| `PerformCrop` | 選択矩形による切り出し |
| `PicPreview_CropMouseDown` | 切出しマウスダウン |
| `PicPreview_CropMouseMove` | 切出しマウス移動 |
| `PicPreview_CropMouseUp` | 切出しマウスアップ |
| `PicPreview_MouseWheel` | マウスホイール（Ctrl+ズーム、Shift+水平スクロール） |
| `StepZoom` | 指定方向に 1 段階ズーム変更 |
| `UpdatePreviewImage` | プレビュー画像の差し替え |
| `UpdateStatusBar` | ステータスバーの更新 |
| `UpdateScrollBars` | スクロールバーの更新 |
| `UpdatePictureBoxZoom` | PictureBox のサイズ・位置更新 |

> **注记**: 上記は `MainForm.cs` のイベントハンドラのうち、キャプチャ・切り出し・ズームに関する主要なもののみを抜粋しています。以下のようなイベントハンドラは記載を省略しています。
>
> - 描画系イベントハンドラ（`PicPreview_Paint`, `PnlPreview_Paint`）
> - ルーペマウスイベントハンドラ（`PicLoupe_MouseMove`, `PicLoupe_MouseDown`, `PicLoupe_MouseUp`）
> - スクロールイベントハンドラ（`VScroll_Scroll`, `HScroll_Scroll`）
> - その他コントロールイベントハンドラ（`BtnSave_Click`, `TxtFileNameTemplate_TextChanged`, `PnlPreview_MouseDown`, `LblZoom_MouseDown` など）

#### 6.1.4 UI コントロール構成

Designer.cs で定義される主要コントロール:

- **MenuStrip**: ファイル、編集、キャプチャ、表示メニュー
- **FlowLayoutPanel**（ツールバー）: キャプチャボタン、保存ボタン、テキストボックス
- **Panel**（プレビュー）: PictureBox、ルーペ PictureBox、スクロールバー
- **StatusStrip**: 拡大率、画像サイズ、保存状態、配置、ルーペ状態
- **ContextMenuStrip**: プレビュー用、ズーム用、配置用、ルーペ用、リンク用

---

### 6.2 `SelectionForm.cs`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `public sealed partial class : Form` |
| **名前空間** | `app.Views` |
| **責務** | 画面キャプチャ時の領域選択用モーダルフォーム。3 種の選択モードをサポート |

#### 6.2.1 コンストラクタ

`SelectionForm(CaptureType captureType, Bitmap preCapturedImage, string borderColorName = "White")`

#### 6.2.2 イベント

| イベント | シグネチャ | 説明 |
| --- | --- | --- |
| `SelectionCompleted` | `EventHandler<Rectangle>` | 選択完了時（引数はスクリーン座標の矩形） |
| `Cancelled` | `EventHandler` | キャンセル時 |

#### 6.2.3 パブリックプロパティ

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `SelectedWindowHandle` | `IntPtr` | ウィンドウ選択モードで確定されたウィンドウハンドル |

#### 6.2.4 内部メソッド

| メソッド | 説明 |
| --- | --- |
| `SelectionForm_Paint` | フォーム描画。モードに応じて適切な描画メソッドを呼び出し |
| `PaintScreenSelect` | スクリーン選択描画。ハイライト + 暗転 |
| `PaintWindowSelect` | ウィンドウ選択描画。ハイライト + ウィンドウ名表示 |
| `PaintAreaSelect` | 領域選択描画。選択矩形 + 座標・サイズ情報表示 |
| `ScreenToClient` | スクリーン座標 → クライアント座標変換 |
| `GetScreenIndexAtPoint` | 指定座標のスクリーンインデックス取得 |
| `DetectWindowUnderCursor` | マウスカーソル下のウィンドウ検出 |
| `CreateDashedBorderPen` | 白黒破線ペン作成 |
| `DrawOverlayText` | オーバーレイテキスト描画 |

---

### 6.3 `SettingsForm.cs`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `public sealed partial class : Form` |
| **名前空間** | `app.Views` |
| **責務** | 動作設定（色・配置・ルーペ・キャプチャ方式・アイコンサイズ）を変更するモーダルダイアログ |

#### 6.3.1 コンストラクタ

`SettingsForm(Settings settings)`

#### 6.3.2 内部メソッド

| メソッド | 説明 |
| --- | --- |
| `LoadSettings` | 設定オブジェクトの値を各コントロールに読み込み |
| `BtnOk_Click` | 各コントロールの値を設定オブジェクトに書き戻し |
| `BtnCancel_Click` | 変更を破棄して閉じる |
| `CmbCaptureMode_SelectedIndexChanged` | キャプチャモード説明文更新 |
| `UpdateCaptureModeDescription` | キャプチャモードの説明ラベル更新 |
| `PickColor` | ColorDialog 表示・色選択 |

---

### 6.4 `HotkeyForm.cs`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `public sealed partial class : Form` |
| **名前空間** | `app.Views` |
| **責務** | グローバルホットキーを設定するモーダルダイアログ |

#### 6.4.1 コンストラクタ

`HotkeyForm(Settings settings, Action saveAction)`

#### 6.4.2 内部メソッド

| メソッド | 説明 |
| --- | --- |
| `UpdateDisplay` | 現在のホットキー設定を表示 |
| `StartCapturing` | キー入力受付モード開始 |
| `ProcessCmdKey` | キーボード入力からホットキー設定を生成 |
| `BtnSetSelectScreen_Click` | スクリーン選択ホットキー設定開始 |
| `BtnSetWindowSelect_Click` | ウィンドウ選択ホットキー設定開始 |
| `BtnSetAreaSelect_Click` | 範囲選択ホットキー設定開始 |

---

### 6.5 `FolderViewForm.cs`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `public sealed partial class : Form` |
| **名前空間** | `app.Views` |
| **責務** | 保存先フォルダの内容を表示するモードレスダイアログ |

#### 6.5.1 コンストラクタ

`FolderViewForm(string folderPath, SettingsService settingsService)`

#### 6.5.2 内部フィールド

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `_viewModel` | `FolderViewViewModel` | ViewModel |
| `_folderPath` | `string` | 表示するフォルダパス |
| `_settings` | `Settings` | 設定オブジェクト |
| `_sortAscending` | `bool` | ソート昇順 |
| `_items` | `List<ListViewItem>` | 表示アイテム一覧 |
| `_largeIconList` | `ImageList` | 特大アイコン用 |
| `_tileIconList` | `ImageList` | 大アイコン用 |
| `_mediumIconList` | `ImageList` | 中アイコン用 |
| `_smallIconList` | `ImageList` | 16x16 アイコン用 |
| `_loadCts` | `CancellationTokenSource?` | 非同期読込キャンセルトークン |
| `_isAsyncLoadEnabled` | `bool` | 非同期読み込み有効フラグ（テスト用） |

#### 6.5.3 内部メソッド

| メソッド | 説明 |
| --- | --- |
| `LoadFolderContents` | フォルダ内容を読み込み、シェルアイコンを表示後、非同期でサムネイルを差し替え |
| `LoadThumbnailsAsync` | 画像サムネイルを非同期で読み込み |
| `LoadThumbnailsSync` | 画像サムネイルを同期で読み込み（テスト用） |
| `ResizeImage` | 画像を指定サイズにリサイズ（アスペクト比維持、中央寄せ） |
| `SortAndAddItems` | アイテムをソートして ListView に追加 |
| `FormatFileSize` | ファイルサイズを人間可読文字列に変換 |
| `GetIconIndex` | ファイル/フォルダのシェルアイコンを取得しキャッシュ |
| `AddResizedIcon` | アイコンを指定サイズにリサイズして ImageList に追加 |

---

## 7. Services

### 7.1 `ISettingsService.cs`

| 分類 | 内容 |
| --- | --- |
| **定義** | `public interface ISettingsService` |
| **名前空間** | `app.Services` |
| **責務** | JSON ファイルによる設定管理のインターフェース |

#### 7.1.1 メソッド

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `Current`（プロパティ） | `Settings { get; }` | 現在の設定オブジェクト |
| `Save` | `void()` | 設定をファイルに保存 |
| `Reload` | `void()` | 設定をファイルから再読み込み |

### 7.2 `SettingsService.cs`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `public sealed class : ISettingsService` |
| **名前空間** | `app.Services` |
| **責務** | JSON ファイルによる設定の永続化 |

#### 7.2.1 内部フィールド

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `s_settingsFilePath` | `string`（static readonly） | 設定ファイルパス（`%LOCALAPPDATA%\SaveEvidence\settings.json`） |
| `s_jsonOptions` | `JsonSerializerOptions`（static readonly） | 整形出力、コメント許容、末尾カンマ許容 |
| `_lock` | `Lock` | 排他制御用ロック |
| `_current` | `Settings` | 現在の設定オブジェクト |

#### 7.2.2 内部メソッド（private static）

| メソッド | 説明 |
| --- | --- |
| `LoadInternal` | JSON ファイルから設定を読み込む。ファイルがない or 読込失敗時はデフォルト設定 |
| `SaveInternal` | 設定を JSON ファイルに書き出す。保存失敗時は続行 |

---

## 8. 非公開 API（internal / private）

### 8.1 MainForm 内部メソッド一覧（抜粋）

| メソッド | 説明 |
| --- | --- |
| `InitializeViewModel` | ViewModel との DataBindings と PropertyChanged 購読を初期化 |
| `RegisterHotKeys` | 現在の設定に基づいて全ホットキーを再登録 |
| `ExecuteCapture(CaptureType)` | 指定キャプチャ種別を実行 |
| `PerformSelectScreenCapture` | スクリーン選択キャプチャ実行 |
| `StartSelectionMode(CaptureType)` | 指定キャプチャ種別の SelectionForm をモーダル表示 |
| `EnsureVisible` | フォームを確実に可視状態に復元 |
| `ExitCropMode` | 切出しモード終了 |
| `ClientToImage(Point)` | PictureBox クライアント座標 → 画像座標変換 |
| `PanelToImage(Point)` | パネル座標 → 画像座標変換 |
| `EventToImage(object?, Point)` | イベント発生源に応じた座標変換 |
| `GetActualZoom` | 実際の表示倍率を計算（自動時はフィット倍率） |
| `GetActualZoomPercent` | 現在の実際の拡大率（%）を取得 |
| `ResetZoomToAuto` | ズームを自動フィットにリセット |
| `SetZoomFromContext(int)` | コンテキストメニューからのズーム設定 |
| `PicPreview_CropPaint` | 選択矩形とハンドルの描画 |
| `PushUndo` | アンドゥスタックに画像を追加 |
| `PerformUndo` | アンドゥ実行 |
| `UpdateMenuStates` | メニュー・コンテキストメニューの有効/無効状態を更新 |
| `NotifyMenuStateChanged` | メニュー状態の更新が必要であることを記録 |
| `ApplyLoupeModeFromSetting` | ルーペ表示モードを設定から反映 |
| `ApplyLoupeSettings` | ルーペサイズ・拡大率を設定から反映 |
| `UpdateLoupePosition` | ルーペ表示位置をマウス位置に更新 |
| `RestoreFormBounds` | 保存されたウィンドウ位置・サイズを復元 |
| `SaveFormBounds` | 現在のウィンドウ位置・サイズを設定に保存 |
| `AdjustFileNameBoxWidth` | ファイル名テキストボックスの幅を調整 |
| `UpdateValidationState` | テンプレート検証状態の表示更新 |
| `GetHandleClientPoints` | 8 方向ハンドルのクライアント座標を計算 |
| `SortFolderItems` | フォルダビューアーのアイテムをソート |

> **注记**: 上記は `MainForm.cs` の private メソッド（全 80 以上）のうち、主要なもののみを抜粋しています。以下のカテゴリのメソッドは記載を省略しています。
>
> - 各メニュークリックハンドラ（`MenuFileSave_Click`, `MenuEditUndo_Click`, `MenuEditCopy_Click` など）— 16 個
> - 各ズーム設定ハンドラ（`CtxZoom25_Click` ～ `CtxZoom500_Click`）— 17 個
> - 各種イベントハンドラ（`BtnSave_Click`, `PicLoupe_MouseMove`, `VScroll_Scroll` など）
> - 内部ユーティリティ（`UpdatePictureBoxZoom`, `GenerateLoupeImage`, `CreateCheckerPattern` など）

---

## 9. 定数一覧

### 9.1 プログラム全体の定数

| 定義場所 | 定数 | 値 | 説明 |
| --- | --- | --- | --- |
| `CropSelection` | `HandleSize` | 24 | ハンドルサイズ（ピクセル） |
| `MainForm` | `MaxUndoCount` | 5 | アンドゥ最大段階数 |
| `MainForm` | `s_zoomValues` | `[0,25,33,...,500]` | ズーム段階一覧（18 段階） |
| `CaptureManager` | `GA_ROOT` | 2 | トップレベルウィンドウ取得フラグ |
| `CaptureManager` | `PW_CLIENTONLY` | 0x00000001 | クライアント領域のみ描画 |
| `CaptureManager` | `PW_RENDERFULLCONTENT` | 0x00000002 | 完全描画要求 |
| `CaptureManager` | `DWMWA_EXTENDED_FRAME_BOUNDS` | 9 | 可視矩形属性 |
| `HotKeyManager` | `WM_HOTKEY` | 0x0312 | ホットキーメッセージ ID |
| `ImageProcessor` | 色類似許容値 | 30 | RGB 差分合計の許容値 |
| `FolderViewForm` | `s_imageExtensions` | .png/.jpg/.jpeg/.bmp/.gif/.tiff/.tif/.webp | 画像ファイル拡張子 |

---

## 10. データ変換仕様

### 10.1 座標変換

| 変換元 | 変換先 | 計算式 |
| --- | --- | --- |
| クライアント座標 | 画像座標 | `imgX = (clientX - offsetX) / zoom`、`imgY = (clientY - offsetY) / zoom`、0～画像サイズ-1 にクランプ |
| スクリーン座標 | SelectionForm クライアント座標 | `clientX = screenX - virtualOrigin.X`、`clientY = screenY - virtualOrigin.Y` |

### 10.2 ファイルサイズ変換

| 範囲 | 表示 |
| --- | --- |
| 0 B ～ 1023 B | `{bytes} B` |
| 1 KB ～ 1023 KB | `{bytes/1024:F1} KB` |
| 1 MB ～ 1023 MB | `{bytes/(1024*1024):F1} MB` |
| 1 GB ～ | `{bytes/(1024*1024*1024):F2} GB` |

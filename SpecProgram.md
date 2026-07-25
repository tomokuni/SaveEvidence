# プログラム詳細仕様書 SpecProgram.md

**SaveEvidence** — 画面キャプチャユーティリティ

---

## 目次

- [1. 名前空間一覧](#1-名前空間一覧)
- [2. Program.cs（エントリポイント）](#2-programcsエントリポイント)
- [3. Enum（列挙型）](#3-enum列挙型)
- [4. Models（モデル）](#4-modelsモデル)
- [5. ViewModels](#5-viewmodels)
- [6. Views（ウィンドウ）](#6-viewsウィンドウ)
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
| `app.Views` | ウィンドウ・UI（Avalonia Window） |
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
| **シグネチャ** | `static void Main(string[] args)` |
| **概要** | アプリケーションのメインエントリポイント。Avalonia の DI コンテナベース起動 |
| **処理フロー** | 1. `BuildAvaloniaApp()` で AppBuilder を構築\<br /\>2. `ServiceCollection` に DI 登録（`SettingsService`, `MainViewModel`, `MainWindow`）\<br /\>3. `UseServiceProvider()` で DI コンテナを Avalonia に統合\<br /\>4. `StartWithClassicDesktopLifetime(args)` でメインループ開始 |

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

### 4.7 `CropSelection.cs`（WinForms バックアップ）

| 分類 | 内容 |
| --- | --- |
| **ファイル位置** | `_WinFormsBackup/CropSelection.cs` |
| **名前空間** | `app.Models` |
| **責務** | プレビュー画像上の手動切り出し選択状態を管理（WinForms版の保管用。Avalonia版では使用しない） |

> **移植注記**: WinForms 版では `CropSelection` クラスが Model 層で選択状態を管理していたが、Avalonia 版では切り出し選択ロジックを `MainWindow.axaml.cs` の分離コードに直接実装している。  
> `_cropStartPoint`, `_cropRect`, `_isCropping` の 3 フィールドで選択状態を管理し、`PointerPressed`/`PointerMoved`/`PointerReleased` イベントで操作を処理する。

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
| `CropPreview` | `void(Rectangle bounds)`（private RelayCommand） | 指定範囲でプレビュー画像を切り出し。実行前に `BeforeEditAction?.Invoke()` を呼び出し |
| `AutoCropWindow` | `void()`（private RelayCommand） | ウィンドウ領域を自動判定して切り出し。実行前に `BeforeEditAction?.Invoke()` を呼び出し |
| `SaveImage` | `bool()` | 現在のプレビュー画像を保存。保存後に数値インクリメント |
| `OpenSaveFolder` | `void()`（private RelayCommand） | 保存先フォルダをエクスプローラーで開く |
| `ReloadSettings` | `void()` | 設定を再読み込み |
| `ZoomIn` | `void()` | ズームを1段階拡大。`ZoomLevels` 配列に従い `CanZoomIn/CanZoomOut` を更新 |
| `ZoomOut` | `void()` | ズームを1段階縮小。`ZoomLevels` 配列に従い `CanZoomIn/CanZoomOut` を更新 |

#### 5.1.5 コールバック

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `BeforeEditAction` | `Action?` | 画像編集（切り出し等）の直前に呼び出されるコールバック。View 側が Undo スタック退避に使用 |
| `StartSelectionMode` | `Action<CaptureType>?` | キャプチャ選択開始コールバック |
| `CopyToClipboardAsync` | `Func<Bitmap?, Task<bool>>?` | クリップボードコピーの非同期コールバック |
| `PasteFromClipboardAsync` | `Func<Task<Bitmap?>>?` | クリップボード貼り付けの非同期コールバック |
| `ConfirmOverwriteAsync` | `Func<string, Task<bool>>?` | 上書き確認ダイアログの非同期コールバック |

#### 5.1.7 定数

| フィールド | 値 | 説明 |
| --- | --- | --- |
| `ZoomLevels` | `[0,25,33,50,67,75,80,90,100,110,125,150,175,200,250,300,400,500]` | ズーム段階一覧（18段階、0=自動フィット） |

#### 5.1.8 partial メソッド（ソースジェネレーター生成）

| メソッド | 説明 |
| --- | --- |
| `OnPreviewImageChanging` | 変更前に古い画像を Dispose |
| `OnFileNameTemplateTextChanged` | テンプレート変更時に設定を保存 |
| `OnSaveFolderPathChanged` | 保存先変更時に設定を保存し表示名を更新 |

#### 5.1.9 内部フィールド

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

## 6. Views（ウィンドウ）

### 6.1 `MainWindow.axaml` / `MainWindow.axaml.cs`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `public sealed partial class : Window` |
| **名前空間** | `app.Views` |
| **責務** | メインウィンドウ。キャプチャ・プレビュー・編集・保存の全機能を統合 |
| **備考** | UI 定義は `MainWindow.axaml`（XAML）、コードビハインドは `MainWindow.axaml.cs` |

#### 6.1.1 依存関係

- `MainViewModel _viewModel` — ViewModel（DI 注入）
- `HotKeyManager? _hotKeyManager` — ホットキー管理
- `SettingsService _settingsService` — 設定サービス（DI 注入）

#### 6.1.2 内部フィールド

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `_isCropping` | `bool` | 切出しモードフラグ |
| `_cropStartPoint` | `Avalonia.Point` | 切出し開始座標 |
| `_cropRect` | `Rect` | 切出し選択矩形 |
| `_isLoupeVisible` | `bool` | ルーペ表示中フラグ |
| `_loupeCts` | `CancellationTokenSource?` | ルーペ更新キャンセルトークン |
| `_undoStack` | `List<Bitmap>` | アンドゥスタック（最大5） |
| `_isUndoing` | `bool` | アンドゥ実行中フラグ |
| `_selectionWindow` | `SelectionWindow?` | 選択ウィンドウ参照 |

#### 6.1.3 主要イベントハンドラ

| ハンドラ | 説明 |
| --- | --- |
| `OnMainWindowKeyDown` | キーボードショートカット（Ctrl+C/V/S/Z, Ctrl++/-） |
| `OnImagePointerWheelChanged` | Ctrl+マウスホイールによるズーム |
| `OnPreviewImagePointerPressed` | 切出しマウスダウン |
| `OnPreviewImagePointerMoved` | 切出しマウス移動 / ルーペ更新 |
| `OnPreviewImagePointerReleased` | 切出しマウスアップ（PerformCrop） |
| `PerformAutoCrop` | 自動ウィンドウ検出切り出し |
| `PerformCrop` | 選択矩形による切り出し |
| `ApplyZoom` | 現在のズーム設定をプレビューに適用 |
| `UpdateImageAlignment` | CenterAlign 設定に応じた画像配置の更新 |
| `PushUndo` | アンドゥスタックに現在の画像を追加 |
| `RestoreUndo` | アンドゥスタックから画像を復元 |
| `SetupViewModelCallbacks` | ViewModel に UI 操作用コールバックを設定 |
| `StartSelectionMode` | SelectionWindow を表示してキャプチャ選択開始 |
| `OnMainWindowClosing` | ウィンドウ終了処理（設定保存・リソース解放） |
| `OnMainWindowActivated` | アクティブ時ルーペ更新再開 |
| `OnMainWindowDeactivated` | 非アクティブ時ルーペ更新停止 |

#### 6.1.4 UI 構成（MainWindow.axaml）

AXAML で定義される主要コントロール:

- **Menu**: ファイル（保存/名前を付けて保存）、編集（コピー/貼付/戻す）、ツール（切出し/自動切出し/ルーペ）、設定（表示設定/ホットキー）
- **ToolBar**: 保存、コピー、貼付、戻す、切出し、自動切出し、ルーペボタン、ズーム +/- コンボボックス
- **ScrollViewer**（プレビュー）: 画像表示（Image コントロール）、切出し選択矩形オーバーレイ
- **StatusBar**: ファイル名、保存状態

---

### 6.2 `SelectionWindow.axaml` / `SelectionWindow.axaml.cs`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `public sealed partial class : Window` |
| **名前空間** | `app.Views` |
| **責務** | 画面キャプチャ時の領域選択用ウィンドウ。全画面透明オーバーレイとして動作し、3 種の選択モードをサポート |
| **備考** | AXAML は透明背景の Window 定義のみ。全描画はコードビハインドの `Renderer` オーバーライドで実行 |

#### 6.2.1 コンストラクタ

`SelectionWindow(CaptureType captureType, Action<System.Drawing.Bitmap?> onComplete)`

- WinForms 版とは異なり `preCapturedImage` パラメーターは不要（Avalonia 版では選択後に Screen 全体からキャプチャ）

#### 6.2.2 内部フィールド

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `_captureType` | `CaptureType` | キャプチャモード |
| `_startPoint` | `Avalonia.Point` | 選択開始座標 |
| `_selectionRect` | `Rect` | 現在の選択矩形 |
| `_isSelecting` | `bool` | 選択中フラグ |
| `_hasSelection` | `bool` | 選択完了フラグ |
| `_onComplete` | `Action<System.Drawing.Bitmap?>` | キャプチャ完了コールバック |

#### 6.2.3 内部メソッド

| メソッド | 説明 |
| --- | --- |
| `OnPointerPressed` | マウスダウン（選択開始/キャンセル判定） |
| `OnPointerMoved` | マウス移動（選択矩形更新） |
| `OnPointerReleased` | マウスアップ（選択確定/キャプチャ実行） |
| `RenderSelectionOverlay` | 選択矩形・ハイライト・暗転の描画（`D2D` 相当を Avalonia 描画で実現） |
| `DetectWindowUnderCursor` | マウスカーソル下のウィンドウ検出 |

---

### 6.3 `SettingsWindow.axaml` / `SettingsWindow.axaml.cs`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `public sealed partial class : Window` |
| **名前空間** | `app.Views` |
| **責務** | 動作設定（色・配置・ルーペ・キャプチャ方式・アイコンサイズ）を変更するダイアログ |

#### 6.3.1 コンストラクタ

`SettingsWindow(Settings settings, SettingsService settingsService)`

- WinForms 版の `SettingsForm(Settings settings)` に `SettingsService` を追加（保存機能をウィンドウ内で完結）

---

### 6.4 `HotkeyWindow.axaml` / `HotkeyWindow.axaml.cs`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `public sealed partial class : Window` |
| **名前空間** | `app.Views` |
| **責務** | グローバルホットキーを設定するダイアログ |

#### 6.4.1 コンストラクタ

`HotkeyWindow(Settings settings, Action saveAction)`

---

### 6.5 `FolderViewWindow.axaml` / `FolderViewWindow.axaml.cs`

| 分類 | 内容 |
| --- | --- |
| **クラス修飾** | `public sealed partial class : Window` |
| **名前空間** | `app.Views` |
| **責務** | 保存先フォルダの内容を表示するモードレスウィンドウ |

#### 6.5.1 コンストラクタ

`FolderViewWindow(string folderPath, SettingsService settingsService)`

#### 6.5.2 内部フィールド

| フィールド | 型 | 説明 |
| --- | --- | --- |
| `_viewModel` | `FolderViewViewModel` | ViewModel |
| `_folderPath` | `string` | 表示するフォルダパス |
| `_settings` | `Settings` | 設定オブジェクト |
| `_sortAscending` | `bool` | ソート昇順 |
| `_loadCts` | `CancellationTokenSource?` | 非同期読込キャンセルトークン |
| `_isAsyncLoadEnabled` | `bool` | 非同期読み込み有効フラグ（テスト用） |

#### 6.5.3 内部メソッド

| メソッド | 説明 |
| --- | --- |
| `LoadFolderContents` | フォルダ内容を読み込み、シェルアイコンを表示後、非同期でサムネイルを差し替え |
| `LoadThumbnailsAsync` | 画像サムネイルを非同期で読み込み |
| `ResizeImage` | 画像を指定サイズにリサイズ（アスペクト比維持、中央寄せ） |
| `SortAndAddItems` | アイテムをソートして ItemsControl に追加 |
| `FormatFileSize` | ファイルサイズを人間可読文字列に変換 |
| `GetIconIndex` | ファイル/フォルダのシェルアイコンを取得しキャッシュ |

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

### 8.1 MainWindow 内部メソッド一覧（抜粋）

| メソッド | 説明 |
| --- | --- |
| `SetupViewModelCallbacks` | ViewModel に UI 操作用コールバック（StartSelectionMode, CopyToClipboardAsync, PasteFromClipboardAsync, ConfirmOverwriteAsync, BeforeEditAction）を設定 |
| `RegisterHotKeys` | 現在の設定に基づいて全ホットキーを再登録 |
| `StartSelectionMode(CaptureType)` | 指定キャプチャ種別の SelectionWindow を表示 |
| `PerformSelectScreenCapture` | スクリーン選択キャプチャ実行 |
| `ApplyZoom` | 現在のズーム率を Image コントロールに適用（自動フィット時は Stretch.Uniform、手動時は Stretch.None + RenderTransform） |
| `UpdateImageAlignment` | CenterAlign 設定に応じて画像の HorizontalAlignment/VerticalAlignment を切替 |
| `ResetZoomToAuto` | ズームを自動フィットにリセット |
| `PushUndo` | 現在の画像をアンドゥスタックに追加（最大5段階） |
| `RestoreUndo` | アンドゥスタックから画像を復元 |
| `PerformAutoCrop` | 自動ウィンドウ検出切り出し |
| `PerformCrop` | 選択矩形による切り出し |
| `ClientToImage(Point)` | クライアント座標 → 画像座標変換 |
| `PanelToImage(Point)` | ScrollViewer 座標 → 画像座標変換 |
| `EventToImage(object?, Point)` | イベント発生源に応じた座標変換 |
| `GetActualZoom` | 実際の表示倍率を計算（自動時はフィット倍率） |
| `GetActualZoomPercent` | 現在の実際の拡大率（%）を取得 |
| `SaveWindowBounds` | 現在のウィンドウ位置・サイズを設定に保存 |
| `RestoreWindowBounds` | 保存されたウィンドウ位置・サイズを復元 |
| `ShowConfirmDialogAsync` | 確認ダイアログの非同期表示 |
| `OnMainWindowClosing` | 終了処理（設定保存、ホットキー解除、Undo スタック破棄） |
| `OnMainWindowKeyDown` | キーボードショートカット処理 |
| `OnImagePointerWheelChanged` | Ctrl+マウスホイールによるズーム処理 |
| `GenerateLoupeImage` | ルーペ表示用の拡大画像を生成 |
| `UpdateLoupePosition` | ルーペ表示位置をマウス位置に追従 |

---

## 9. 定数一覧

### 9.1 プログラム全体の定数

| 定義場所 | 定数 | 値 | 説明 |
| --- | --- | --- | --- |
| `CropSelection` | `HandleSize` | 24 | ハンドルサイズ（ピクセル） |
| `MainWindow` | `MaxUndoCount` | 5 | アンドゥ最大段階数 |
| `MainWindow` / `MainViewModel` | `s_zoomValues` / `ZoomLevels` | `[0,25,33,...,500]` | ズーム段階一覧（18 段階） |
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

# PlatformFactory 外部機能仕様書

## 目次

- [PlatformFactory 外部機能仕様書](#platformfactory-外部機能仕様書)
  - [目次](#目次)
  - [1. 文書概要](#1-文書概要)
  - [2. 目的](#2-目的)
  - [3. 外部利用条件](#3-外部利用条件)
    - [3.1 プロジェクト参照](#31-プロジェクト参照)
    - [3.2 必要な名前空間](#32-必要な名前空間)
  - [4. 基本的な使用方法](#4-基本的な使用方法)
    - [4.1 プラットフォームサービスの生成](#41-プラットフォームサービスの生成)
    - [4.2 座標変換ポート](#42-座標変換ポート)
    - [4.3 画面座標変換](#43-画面座標変換)
    - [4.4 UI ハング監視（Watchdog）](#44-ui-ハング監視watchdog)
  - [5. 公開API](#5-公開api)
    - [5.1 プロパティ](#51-プロパティ)
      - [5.1.1 CoordinatePort.Identity（静的プロパティ）](#511-coordinateportidentity静的プロパティ)
    - [5.2 メソッド](#52-メソッド)
      - [5.2.1 PlatformFactory（静的ファクトリ）](#521-platformfactory静的ファクトリ)
      - [5.2.2 CoordinatePort（静的ファクトリ）](#522-coordinateport静的ファクトリ)
      - [5.2.3 ScreenConverter](#523-screenconverter)
      - [5.2.4 Watchdog](#524-watchdog)
      - [5.2.5 ColorExtensions（C# 14 静的拡張メンバー）](#525-colorextensionsc-14-静的拡張メンバー)
      - [5.2.6 PbBitmap（ピクセル操作）と PbBitmapExtensions（生成・変換）](#526-unifiedbitmapピクセル操作と-unifiedbitmapextensions生成変換)
    - [5.3 イベント](#53-イベント)
    - [5.4 型](#54-型)
      - [5.4.1 PlatformFactory](#541-platformfactory)
      - [5.4.2 インターフェース（7 種）](#542-インターフェース7-種)
      - [5.4.3 CoordinatePort](#543-coordinateport)
      - [5.4.4 ScreenConverter](#544-screenconverter)
      - [5.4.5 Watchdog](#545-watchdog)
      - [5.4.6 その他の公開型](#546-その他の公開型)
  - [6. 座標系](#6-座標系)
    - [6.1 3 層の座標](#61-3-層の座標)
    - [6.2 CoordinatePort の変換規則](#62-coordinateport-の変換規則)
  - [7. ログ](#7-ログ)
    - [7.1 LogLevel 設定ポリシー](#71-loglevel-設定ポリシー)
    - [7.2 ログ定義一覧](#72-ログ定義一覧)
  - [8. 制約事項](#8-制約事項)
  - [9. 最適化施策](#9-最適化施策)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `PlatformFactory` |
| プロジェクト | `PlatformFactory/PlatformFactory.csproj` |
| 名前空間 | `Estable.AvaloniaUI.Platform`（画像ユーティリティは `Estable.AvaloniaUI.Platform.Imaging`） |
| 対象フレームワーク | `net10.0` |
| UIフレームワーク | Avalonia 12.1 |
| 主要設計パターン | Factory Method（PlatformFactory）/ Func 委譲による DIP ポート（CoordinatePort）/ P/Invoke 集約（WinNative） |
| 主な用途 | 実行時プラットフォームに応じた既定実装の生成と、座標変換・画面キャプチャ・要素検出の抽象化 API の提供 |

## 2. 目的

`PlatformFactory` は、実行時プラットフォーム（Windows / 他環境）に応じたプラットフォーム分岐を一元化し、プラットフォーム固有実装（Win32 API ベース）とフォールバック実装（NoOp）を抽象（インターフェース）経由で提供します。

次の機能を提供します。

- プラットフォームに応じた既定実装の生成（`PlatformFactory.CreateXxx()`）
- カーソル操作（`ICursorService`）
- 要素検出（`IElementFinder`）
- カーソル上書き抑止（`ICursorOverrideGuard`）
- 画面キャプチャ（`ICaptureService`）
- ネイティブウィンドウ操作（`IWindowService`）
- グローバルホットキー（`IGlobalHotKeyService<T>`）
- 座標変換ポート（`ICoordinatePort` / `CoordinatePort` / `ScreenConverter`）
- UI ハング監視（`Watchdog`）
- 画像ユーティリティ（`BitmapExtensions` / `PbBitmap` / `PbBitmapExtensions` / `ImageProcessor`）

## 3. 外部利用条件

### 3.1 プロジェクト参照

```xml
<ProjectReference Include="..\PlatformFactory\src\PlatformFactory.csproj" />
```

### 3.2 必要な名前空間

```csharp
using Estable.AvaloniaUI.Platform;          // PlatformFactory / ICoordinatePort / CoordinatePort / ScreenConverter / Watchdog 等
using Estable.AvaloniaUI.Platform.Imaging;  // BitmapExtensions / PbBitmap / PbBitmapExtensions / ImageProcessor 等
```

## 4. 基本的な使用方法

### 4.1 プラットフォームサービスの生成

```csharp
var cursorService = PlatformFactory.CreateCursorService();
var captureService = PlatformFactory.CreateCaptureService(logger);
var elementFinder = PlatformFactory.CreateElementFinder();
var windowService = PlatformFactory.CreateWindowService();
var cursorOverrideGuard = PlatformFactory.CreateCursorOverrideGuard();
var hotKeyService = PlatformFactory.CreateGlobalHotKeyService<MyActionType>();
```

ホスト（上位 AP）が独自実装を持つ場合は、本ファクトリを使わず各インターフェースに直接注入します（DIP）。

### 4.2 座標変換ポート

```csharp
// 等価変換（1 ドメイン = 1 DIP・無制限）
ICoordinatePort identity = CoordinatePort.Identity;

// スクロールオフセット平行移動（コンテンツ ↔ ビューポート）
ICoordinatePort scroll = CoordinatePort.ScrollOffset(() => scrollViewer.Offset);

// ドメイン = ローカル DIP × 倍率（呼び出し毎評価）
ICoordinatePort scale = CoordinatePort.DomainScale(() => renderScaling);

// 画像レイアウト（レターボックス・Uniform スケール）。Forward = DIP → 論理（元画像ピクセル）
ICoordinatePort layout = CoordinatePort.ImageLayout(() => viewport.Bounds.Size, sourceSize);

// 2 ポート合成（inner 先行 → outer 後続）
ICoordinatePort composed = CoordinatePort.Compose(layout, scroll);
```

変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化を反映できます。

### 4.3 画面座標変換

```csharp
var screenPt = ScreenConverter.LocalDipToScreen(control, localDip); // ローカル DIP → 画面物理
var localDip = ScreenConverter.ScreenToLocalDip(control, screenPt); // 画面物理 → ローカル DIP

// コントロールローカル DIP ↔ 画面物理の動的変換ポート（TopLevel を毎回解決）
ICoordinatePort dipPort = ScreenConverter.CreateVisualDipPort(control);
```

### 4.4 UI ハング監視（Watchdog）

```csharp
var watchdog = new Watchdog(logger, windowService);
watchdog.Start(overlayWindow);   // 監視開始（1 秒間隔ハートビート + 5 秒タイムアウト監視）
// UI スレッドのイベントハンドラから生存通知
watchdog.Pulse();
watchdog.Stop();                 // 監視停止
```

UI スレッドが完全にハングした場合、バックグラウンドスレッドから `IWindowService.EvacuateWindow` でオーバーレイを退避します（最終保険）。

## 5. 公開API

公開 API は「プロパティ / メソッド / イベント / 型」の 4 分類で構成されます。
座標を扱う API は、ドメイン座標系 / DIP 座標系 / 画面物理 Pixel のどれであるかを明記します。

### 5.1 プロパティ

#### 5.1.1 CoordinatePort.Identity（静的プロパティ）

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `Identity` | `static ICoordinatePort Identity { get; }` | 等価変換（A = B）の既定ポート。変換対象の座標系はポート利用時に決定される（例: ドメイン座標系 ↔ DIP 座標系） |

### 5.2 メソッド

#### 5.2.1 PlatformFactory（静的ファクトリ）

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `CreateCursorService` | `ICursorService CreateCursorService(ILogger? logger = null)` | 既定のカーソル操作サービスを生成 |
| `CreateElementFinder` | `IElementFinder CreateElementFinder()` | 既定の要素検出サービスを生成 |
| `CreateCursorOverrideGuard` | `ICursorOverrideGuard CreateCursorOverrideGuard()` | 既定のカーソル上書き抑止ガードを生成 |
| `CreateCaptureService` | `ICaptureService CreateCaptureService(ILogger? logger = null)` | 既定のキャプチャサービスを生成 |
| `CreateWindowService` | `IWindowService CreateWindowService(ILogger? logger = null)` | 既定のネイティブウィンドウ操作サービスを生成 |
| `CreateGlobalHotKeyService` | `IGlobalHotKeyService<T> CreateGlobalHotKeyService<T>() where T : notnull` | 既定のグローバルホットキーサービスを生成 |

#### 5.2.2 CoordinatePort（静的ファクトリ）

| メンバー | シグネチャ | 説明 |
| --- | --- | --- |
| `Compose` | `static ICoordinatePort Compose(ICoordinatePort outer, ICoordinatePort inner)` | 2 ポート合成（inner 先行 → outer 後続。Bounds は outer 優先） |
| `ScrollOffset` | `static ICoordinatePort ScrollOffset(Func<Vector> offsetProvider)` | スクロールオフセット平行移動（コンテンツ ↔ ビューポート。両者とも DIP 座標系） |
| `Scale` | `static ICoordinatePort Scale(double factor)` | 原点基準の等方スケーリング（factor ≤ 0 は 1.0 扱い・固定値） |
| `DomainScale` | `static ICoordinatePort DomainScale(Func<double> factorProvider)` | ドメイン座標系 = ローカル DIP 座標系 × 倍率（呼び出し毎評価） |
| `ImageLayout` | `static ICoordinatePort ImageLayout(Func<Size> viewportSizeProvider, Size sourceSize, Func<bool>? uniformProvider = null)` | 画像レイアウト（レターボックス・スケール）の動的ポート（Forward = DIP 座標系 → 論理 = ドメイン座標系（元画像ピクセル）、Bounds = 元画像サイズ） |
| `ImageLayoutScaleX` | `static double ImageLayoutScaleX(Func<Size> viewportSizeProvider, Size sourceSize, Func<bool>? uniformProvider = null)` | 画像レイアウトの X スケール（ズーム率導出用） |
| コンストラクタ | `CoordinatePort(Func<Point, Point> forward, Func<Point, Point> inverse, Func<Size?> bounds)` | 変換関数を直接注入（座標系は利用側が定義） |

#### 5.2.3 ScreenConverter

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `LocalDipToScreen` | `PixelPoint LocalDipToScreen(Visual source, Point local)` | コントロールローカル DIP 座標系 → 画面物理 Pixel（`TopLevel.PointToScreen` 委譲） |
| `ScreenToLocalDip` | `Point ScreenToLocalDip(Visual source, PixelPoint screen)` | 画面物理 Pixel → コントロールローカル DIP 座標系 |
| `CreateVisualDipPort` | `ICoordinatePort CreateVisualDipPort(Visual source)` | コントロールローカル DIP 座標系 ↔ 画面物理 Pixel の動的ポート（TopLevel を毎回解決） |

#### 5.2.4 Watchdog

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `Start` | `void Start(TopLevel window)` | 監視開始（1 秒間隔ハートビート + バックグラウンド 5 秒タイムアウト監視） |
| `Stop` | `void Stop()` | 監視停止 |
| `Pulse` | `void Pulse()` | UI スレッド生存通知（イベントハンドラから呼ぶ） |

#### 5.2.5 ColorExtensions（C# 14 静的拡張メンバー）

| メソッド | シグネチャ | 説明 |
| --- | --- | --- |
| `FromAColor` | `static Color FromAColor(byte alpha, Color baseColor)` | 基準色（名前付き色等）と不透明度から色を生成する。既存の `Color.FromArgb(alpha, r, g, b)` は RGB 直指定で何色か判別しにくいため、基準色を指定して可読性を向上する（例: `Color.FromAColor(128, Colors.Black)` = 黒 50%） |

#### 5.2.6 PbBitmap（ピクセル操作）と PbBitmapExtensions（生成・変換）

`PbBitmap` は内部表現（`SKBitmap` BGRA8888・Premul）と、`SkiaSharp` のみに依存するピクセル操作・生成/変換（`Create` / `FromSKBitmap` / `ToSKBitmap`）を提供するコア型です。  
Avalonia / System.Drawing 依存の生成・変換は `PbBitmapExtensions`（`this Bitmap` / `this DrawingBitmap` と `this PbBitmap` の拡張メソッド）に分離され、コアは画像種別（Avalonia / System.Drawing）に依存しません。  

**PbBitmap（コア）**:

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| 生成 | `Create` | `static PbBitmap Create(int width, int height)` | 指定サイズの空ビットマップ（BGRA8888・Premul）を生成。`SkiaSharp` のみに依存 |
| 生成 | `FromSKBitmap` | `static PbBitmap FromSKBitmap(SKBitmap bitmap)` | 内部 `SKBitmap` から生成（**所有権を譲り受け**、破棄に連動）。`SkiaSharp` のみに依存 |
| プロパティ | `PixelWidth` / `PixelHeight` | `int` | 幅 / 高さ（ピクセル） |
| プロパティ | `Stride` | `int` | 1 ラインあたりバイト数（パディング含む） |
| プロパティ | `IsDisposed` | `bool` | 破棄済みか |
| ピクセル | `GetPixel` | `(byte B, byte G, byte R, byte A) GetPixel(int x, int y)` | 指定座標の BGRA（アルファ乗算済み）を取得。範囲外は `IndexOutOfRangeException` |
| ピクセル | `SetPixel` | `void SetPixel(int x, int y, byte b, byte g, byte r, byte a)` | 指定座標の BGRA（アルファ乗算済み）を設定 |
| ピクセル | `Clear` | `void Clear(byte b, byte g, byte r, byte a)` | 全ピクセルを指定色（BGRA）で塗りつぶし |
| 変換 | `ToSKBitmap` | `SKBitmap ToSKBitmap()` | 内部インスタンスを返す（共有参照。呼び出し側で Dispose しない）。`SkiaSharp` のみに依存 |
| その他 | `Clone` | `PbBitmap Clone()` | 独立した複製を生成（所有権は呼び出し側） |

**PbBitmapExtensions（生成・変換）**:

| 種別 | メンバー | シグネチャ | 説明 |
| --- | --- | --- | --- |
| 生成 | `ToPbBitmap` | `PbBitmap ToPbBitmap(this Bitmap bitmap)` | Avalonia `Bitmap` から生成（複製。元は変更されない。`CopyPixels` ベースで高速・可逆） |
| 生成 | `ToPbBitmap` | `PbBitmap ToPbBitmap(this DrawingBitmap bitmap)` | `System.Drawing.Bitmap` から生成（複製。**Windows 専用**） |
| 変換 | `Size` | `PixelSize Size()` | 画像サイズ（Avalonia `PixelSize`）。`this PbBitmap` のインスタンス拡張 |
| 変換 | `ToAvaloniaBitmap` | `Bitmap ToAvaloniaBitmap()` | Avalonia `Bitmap` へ変換（複製を所有。`WriteableBitmap.Lock` ベース） |
| 変換 | `ToImage` | `IImage ToImage()` | Avalonia の画像入力（`IImage`）として公開するアダプタ（`PbBitmapImage`）を生成（**ゼロコピー・複製なし**） |
| 変換 | `ToSystemDrawingBitmap` | `DrawingBitmap ToSystemDrawingBitmap()` | `System.Drawing.Bitmap`（Format32bppPArgb）へ変換（複製。**Windows 専用**） |
| ピクセル | `Clear` | `void Clear(AvaloniaColor color)` | 全ピクセルを指定色（Avalonia Color）で塗りつぶし（アルファ乗算） |

> `FromSKBitmap` は引数の所有権を譲り受け、本型の破棄時に `Dispose` します。`ToSKBitmap` は内部インスタンスをそのまま返すため呼び出し側で `Dispose` しないでください。`ToAvaloniaBitmap` / `ToSystemDrawingBitmap` は複製を返すため呼び出し側が所有します。`ToImage` は内包する `PbBitmap` の所有権を生成した `PbBitmapImage` へ移すため、呼び出し側は元の `PbBitmap` を破棄しないでください。

`PbBitmapImage` は `PbBitmap` を内包して `IImage` として公開するアダプタです。`Draw` は `ISkiaSharpApiLeaseFeature` を取得して `SKCanvas` へ直接描画します（`WriteableBitmap` / `Bitmap` を生成しないゼロコピー描画）。破棄は `Dispose` で行い、内包 `PbBitmap` の所有権を保持します。

### 5.3 イベント

公開イベントとして、`IGlobalHotKeyService<T>.HotKeyPressed`（WM_HOTKEY 受信時のアクション配信）があります。その他はサービスの生成・座標変換・画面キャプチャの抽象提供のみで、結果は戻り値で通知します。

### 5.4 型

公開型は `PlatformFactory` / 各インターフェース（`ICursorService` / `IElementFinder` / `ICursorOverrideGuard` / `ICaptureService` / `IWindowService` / `IGlobalHotKeyService<T>` / `ICoordinatePort`）/ `CoordinatePort` / `ScreenConverter` / `Watchdog` / `ElementFinderResults` / `BitmapExtensions` / `PbBitmap` / `PbBitmapImage` / `PbBitmapExtensions` / `ImageProcessor` です。
プラットフォーム固有実装（Win / NoOp 系）と内部ユーティリティ（`CoordinateMath` / `PlatformLog`）はすべて internal（非公開）です。公開 API はプラットフォーム非依存型のみを使用します。

#### 5.4.1 PlatformFactory

公開静的ファクトリ（`public static class`・Factory Method パターン）。生成メソッドは「5.2.1 メソッド」に記載の通りです。

#### 5.4.2 インターフェース（7 種）

| インターフェース | メンバー | 説明 |
| --- | --- | --- |
| `ICursorService` | `PixelPoint GetCursorScreenPoint()` | 現在のカーソル位置を画面物理 Pixel で取得 |
| | `void SetCursorScreenPoint(PixelPoint point)` | カーソルを画面物理 Pixel へ移動 |
| | `void ShowCursor(bool show)` | マウスポインターの表示 / 非表示切替 |
| `IElementFinder` | `ElementFinderResults FindWindowUnderCursor(PixelPoint screenPoint)` | 指定位置（画面物理 Pixel）の最前面ウィンドウ検出。ウィンドウ矩形は最大化時 = ワークエリア（`GetWindowRect`）/ 通常時 = DWM 影除外（`DWMWA_EXTENDED_FRAME_BOUNDS`）。タスクバー / デスクトップウィンドウ（クラス名: `Shell_TrayWnd` / `Shell_SecondaryTrayWnd` / `Shell_ImmersiveTrayWnd` / `Progman` / `WorkerW`）は選択対象外。**タイトル空の `ApplicationFrameWindow`**（スタートメニュー等の UWP オーバーレイ）も除外（タイトル付きの実際の UWP アプリは選択対象） |
| | `ElementFinderResults FindScreenUnderCursor(PixelPoint screenPoint)` | 指定位置（画面物理 Pixel）が含まれるスクリーン検出 |
| | `IReadOnlyList<ElementFinderResults> ListScreens()` | 全スクリーン一覧（仮想スクリーン 1 枚画像の管理用） |
| | `IReadOnlyList<ElementFinderResults> ListWindows()` | 全ウィンドウ一覧 |
| `ICursorOverrideGuard` | `bool IsAttached { get; }` | 抑止がアタッチ済みか |
| | `void Attach(TopLevel window)` | 指定ウィンドウにカーソル上書き抑止をアタッチ（アタッチ中は常に抑止） |
| `ICaptureService` | `Bitmap? CaptureArea(PixelRect bounds)` | 指定画面領域（画面物理 Pixel 座標）をキャプチャ |
| | `Bitmap? CaptureVirtualScreen(out PixelPoint origin)` | 仮想スクリーン全体を 1 回のキャプチャで取得（origin は画面物理 Pixel） |
| `IWindowService` | `void HideFromTaskSwitcher(IPlatformHandle handle)` | タスクバー / Alt+Tab 非表示（選択オーバーレイ用） |
| | `bool SetCaptureExclusion(IPlatformHandle handle, bool exclude)` | キャプチャ除外 / 解除（Windows 10 2004+ のみ true） |
| | `void EvacuateWindow(IPlatformHandle handle)` | 最前面解除・非表示・クローズ（UI ハング時の退避） |
| `IGlobalHotKeyService<T>` | `void Initialize(IntPtr hWnd)` | 初期化（WndProc サブクラス化で WM_HOTKEY 受信） |
| | `void RegisterAll(IReadOnlyList<(T Action, HotKeySetting Setting)> registrations)` | ホットキーとアクションの対応を登録 |
| | `void UnregisterAll()` | 全ホットキーを解除 |
| | `event Action<T>? HotKeyPressed` | WM_HOTKEY 受信時にアクションを配信 |
| `ICoordinatePort` | `Point Forward(Point p)` | 順変換（座標系 A → B。座標系はポート定義による） |
| | `Point Inverse(Point p)` | 逆変換（座標系 B → A。座標系はポート定義による） |
| | `Size? Bounds { get; }` | 座標系 B の境界（null = 無制限） |

#### 5.4.3 CoordinatePort

公開座標変換ポート（`public sealed class : ICoordinatePort`）。プロパティは「5.1.1」、生成メソッドは「5.2.2」に記載の通りです。

#### 5.4.4 ScreenConverter

公開静的クラス（`public static class`）。メソッドは「5.2.3」に記載の通りです。

#### 5.4.5 Watchdog

公開クラス（`public sealed class`・Primary Constructor `Watchdog(ILogger?, IWindowService)`）。メソッドは「5.2.4」に記載の通りです。

#### 5.4.6 その他の公開型

| 型 | 説明 |
| --- | --- |
| `ElementFinderResults` record struct | 検出要素の結果（`Bounds`（画面物理 Pixel）/ `IsDetected` / `Title` / `Handle` / `ClassName`） |
| `BitmapExtensions.CopyRegionWithPadding` | 範囲外許容で領域コピー（クリップ + オフセット、範囲外は透明のまま。座標はピクセル単位） |
| `BitmapExtensions.ExtractRegion` | 矩形切り出し（範囲外は透明パディング。座標はピクセル単位。w/h ≤ 0 は `ArgumentOutOfRangeException`） |
| `ColorExtensions.FromAColor` | C# 14 静的拡張メンバー。基準色（名前付き色等）とアルファ値から `Color` を生成（例: `Color.FromAColor(128, Colors.Black)` = 黒 50%） |
| `PbBitmap` | 複数ビットマップ種別を横断する軽量ユニバーサルラッパー（コア）。内部に `SKBitmap`（BGRA8888・Premul）を唯一所有し、`SkiaSharp` のみに依存したピクセル操作と `Create` / `FromSKBitmap` / `ToSKBitmap` を提供 |
| `PbBitmapExtensions` | `PbBitmap` の生成・変換拡張（`this Bitmap` / `this DrawingBitmap` / `this PbBitmap`）。Avalonia `Bitmap` / `System.Drawing.Bitmap` との相互変換を提供 |
| `ImageProcessor` | 画像の切り出し・ウィンドウ領域検出を提供する静的ユーティリティ。`PbBitmap` に対してピクセルレベルの操作を行う（`Crop` / `DetectAndCropWindow` / `DetectWindowBounds`） |

## 6. 座標系

### 6.1 3 層の座標

| 座標系 | 用途 |
| --- | --- |
| ドメイン座標 | 選択状態・nudge・イベント通知（上位 AP が定義） |
| コントロールローカル DIP | マウス入力・描画（Avalonia 論理座標） |
| 画面物理 Pixel | カーソル操作・キャプチャ・画面変換 |

ドメイン ↔ DIP の変換は `ICoordinatePort`、ローカル DIP ↔ 画面物理は `ScreenConverter` が担います。

### 6.2 CoordinatePort の変換規則

| ファクトリ | Forward（A → B） | Inverse（B → A） | Bounds |
| --- | --- | --- | --- |
| `Identity` | 等価 | 等価 | null |
| `ScrollOffset` | オフセット減算（コンテンツ ↔ ビューポート） | オフセット加算 | null |
| `Scale` | × factor | ÷ factor | null |
| `DomainScale` | × 倍率（毎回評価） | ÷ 倍率（毎回評価） | null |
| `ImageLayout` | DIP → 論理（レターボックス考慮） | 論理 → DIP | 元画像サイズ |
| `Compose(outer, inner)` | inner 先行 → outer 後続 | outer 先行 → inner 後続 | outer 優先 |

## 7. ログ

本プロジェクトのログ定義は `PlatformLog.cs`（internal・LoggerMessage ソース生成）と `WinCaptureService.Log.cs`（private）に集約し、`ILogger` の拡張メソッドとして定義する。
各メソッドは `this ILogger` 第一引数で定義し、呼び出し側では logger 引数を省略する（例: `_log.LogCursorVisibilityChanged(show)`）。
各サービスのコンストラクタ / ファクトリメソッド（`PlatformFactory.CreateXxx(ILogger? logger = null)`）でロガーを注入する。null の場合は出力しない。
出力先・レベル制御はホストアプリケーションが所有する（本クラスは定義のみ）。

### 7.1 LogLevel 設定ポリシー

トレースログは LogLevel.Trace に限定せず、「重要度 × 発生頻度」でレベルを使い分ける。

| LogLevel | 対象 | 本番での意図 |
| --- | --- | --- |
| `Error` | 例外・失敗（処理継続不可） | 常時出力し、障害を確実に残す |
| `Warning` | 異常だがフォールバックで継続（ハング退避・キャプチャ失敗等） | 常時出力し、異常発生の痕跡を残す |
| `Information` | 重要な状態遷移（低頻度：カーソル表示切替・ウィンドウ操作） | 常時出力し、操作手順の再現を可能にする |
| `Debug` | 操作イベントの詳細（キャプチャ内部フロー） | 障害時のみ有効化して解析 |
| `Trace` | 高頻度・極詳細 | 特別な性能・座標解析時のみ有効化 |

判断基準:

1. 例外・警告は常時残す（`Error` / `Warning`）
2. 重要な状態遷移（低頻度）は `Information`
3. 操作イベントの詳細（中頻度）は `Debug`
4. 高頻度・極詳細は `Trace`
5. カーソル位置取得（`GetCursorScreenPoint`）等の高頻度呼び出しはログ対象外（ノイズ防止）

### 7.2 ログ定義一覧

| レベル | メソッド | 出力内容 |
| --- | --- | --- |
| `Warning` | `PlatformLog.LogWatchdogEvacuate` | UI ハング検知によるオーバーレイ退避 |
| `Warning` | `PlatformLog.LogWindowEvacuated` | ハング時のウィンドウ退避（最前面解除・非表示・WM_CLOSE） |
| `Information` | `PlatformLog.LogCursorVisibilityChanged` | カーソル表示/非表示の状態変更 |
| `Information` | `PlatformLog.LogWindowHiddenFromTaskSwitcher` | タスクバー / Alt+Tab からの除外 |
| `Information` | `PlatformLog.LogCaptureExclusionChanged` | キャプチャ除外設定の変更（成否込み） |
| `Debug` | `WinCaptureService.LogVirtualScreenBounds` | 仮想スクリーン全体キャプチャの矩形 |
| `Debug` | `WinCaptureService.LogCaptureSdBitmap` / `LogCaptureSdBitmapDone` | 仮想スクリーン Bitmap キャプチャの開始 / 完了 |

## 8. 制約事項

- Windows 実装（`WinCaptureService` 等）は `[SupportedOSPlatform("windows")]` 属性付きです。他環境では NoOp 実装（安全なフォールバック）が使用されます。
- `ICaptureService.CaptureVirtualScreen` の返却座標は仮想スクリーン座標です（プライマリが (0,0) でない場合 X / Y は負値になり得ます）。
- `IWindowService.SetCaptureExclusion` は Windows 10 2004+ のみ `true` を返します（それ以前はキャプチャ除外できません）。
- `Watchdog` は UI スレッド完全ハング時の最終保険であり、通常のタイムアウト処理には使用しません（通常処理は上位 AP の安全タイマーが担います）。
- `CoordinatePort.Scale` は factor ≤ 0 を 1.0 として扱います（縮小・反転はしません）。
- `BitmapExtensions.ExtractRegion` は幅・高さ ≤ 0 の場合 `ArgumentOutOfRangeException` をスローします。

## 9. 最適化施策

| 施策 | 内容 |
| --- | --- |
| NoOp 実装のシングルトン化 | `NoOpCursorService` / `NoOpElementFinder` / `NoOpCaptureService` / `NoOpWindowService` は `Instance` シングルトンを返し、インスタンス生成コストを回避 |
| プラットフォーム判定の一元化 | `OperatingSystem.IsWindows()` は生成時にのみ評価（キャッシュ不可能な OS 問い合わせのため、頻繁な呼び出しを避ける） |
| Func 委譲による動的ポート | `CoordinatePort` の変換関数は呼び出し毎に評価され、ズーム・スクロールの動的変化を状態コピーなしで反映（オブジェクト生成を抑制） |
| 画像レイアウト計算の一元化 | `CoordinateMath.ComputeImageRenderInfo` に描画情報計算を集約し、`ImageLayout` / `ImageLayoutScaleX` で共用（DRY） |
| ログのソース生成 | `PlatformLog` は LoggerMessage ソース生成（`[LoggerMessage]`）により、ボックス化・文字列補間のオーバーヘッドを回避 |
| キャプチャのバックグラウンド実行 | 仮想スクリーン全体のキャプチャはバックグラウンドスレッド + タイムアウト（上位 AP が制御）で実行し、UI スレッドをブロックしない |
